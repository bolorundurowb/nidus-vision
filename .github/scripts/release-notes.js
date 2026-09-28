// Builds GitHub release notes for the tag that triggered the Publish workflow.
//
// Runs inside actions/github-script. It walks the commits between the previous
// tag and the current one, resolves the pull request each commit was merged
// through, and renders a Markdown body grouped by pull request with the commits
// that made up each PR nested beneath it. Commits that were pushed directly
// (no associated PR) are listed in their own section.

const MAX_BODY_LENGTH = 120_000; // GitHub caps release bodies at 125,000 chars.
const MAX_PR_BODY_LENGTH = 1_500;

module.exports = async ({ github, context, core, exec }) => {
  const { owner, repo } = context.repo;
  const tag = context.ref.replace(/^refs\/tags\//, "");
  const image = process.env.DOCKER_IMAGE ?? `${owner}/${repo}`;

  const previousTag = await findPreviousTag(exec, tag);
  const range = previousTag ? `${previousTag}..${tag}` : tag;
  core.info(`Collecting commits for ${range}`);

  const commits = await listCommits(exec, range);
  core.info(`Found ${commits.length} non-merge commits`);

  const { pullRequests, directCommits } = await groupByPullRequest(github, owner, repo, commits, core);

  const body = truncate(
    renderBody({ owner, repo, tag, previousTag, image, pullRequests, directCommits }),
    MAX_BODY_LENGTH,
  );

  const release = await upsertRelease(github, owner, repo, tag, body, core);

  await core.summary
    .addHeading(`Release ${tag}`)
    .addLink("View release", release.html_url)
    .addRaw("\n\n")
    .addRaw(body)
    .write();

  core.setOutput("release-url", release.html_url);
  core.setOutput("previous-tag", previousTag ?? "");
};

async function findPreviousTag(exec, currentTag) {
  // Tags sorted newest-first by version. The entry after the current tag is the
  // previous release; if the current tag is the oldest there is no baseline.
  const { stdout } = await exec.getExecOutput("git", ["tag", "--sort=-v:refname", "--merged", currentTag]);
  const tags = stdout.split("\n").map((t) => t.trim()).filter(Boolean);
  const index = tags.indexOf(currentTag);
  if (index === -1) return tags[0] ?? null;
  return tags[index + 1] ?? null;
}

async function listCommits(exec, range) {
  const separator = "\u001f";
  const format = ["%H", "%h", "%s", "%an", "%aI"].join(separator);
  const { stdout } = await exec.getExecOutput("git", ["log", "--no-merges", `--format=${format}`, range]);

  return stdout
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [sha, shortSha, subject, author, date] = line.split(separator);
      return { sha, shortSha, subject: cleanSubject(subject), author, date };
    });
}

async function groupByPullRequest(github, owner, repo, commits, core) {
  const pullRequests = new Map();
  const directCommits = [];

  for (const commit of commits) {
    let pulls = [];
    try {
      const response = await github.rest.repos.listPullRequestsAssociatedWithCommit({
        owner,
        repo,
        commit_sha: commit.sha,
      });
      pulls = response.data.filter((pr) => pr.merged_at);
    } catch (error) {
      core.warning(`Could not resolve pull requests for ${commit.shortSha}: ${error.message}`);
    }

    if (pulls.length === 0) {
      directCommits.push(commit);
      continue;
    }

    // Prefer the earliest merged PR when a commit belongs to several (e.g. stacked branches).
    pulls.sort((a, b) => new Date(a.merged_at) - new Date(b.merged_at));
    const pr = pulls[0];

    if (!pullRequests.has(pr.number)) {
      pullRequests.set(pr.number, {
        number: pr.number,
        title: pr.title.trim(),
        url: pr.html_url,
        author: pr.user?.login ?? null,
        body: (pr.body ?? "").trim(),
        mergedAt: pr.merged_at,
        labels: (pr.labels ?? []).map((label) => label.name),
        commits: [],
      });
    }

    pullRequests.get(pr.number).commits.push(commit);
  }

  const ordered = [...pullRequests.values()].sort((a, b) => new Date(b.mergedAt) - new Date(a.mergedAt));
  return { pullRequests: ordered, directCommits };
}

function renderBody({ owner, repo, tag, previousTag, image, pullRequests, directCommits }) {
  const repoUrl = `https://github.com/${owner}/${repo}`;
  const lines = [];

  lines.push("## Docker image", "");
  lines.push("```bash");
  lines.push(`docker pull ${image}:${tag}`);
  lines.push("```", "");
  lines.push(`\`${image}:latest\` also points at this release.`, "");

  const prCommitCount = pullRequests.reduce((sum, pr) => sum + pr.commits.length, 0);
  const totalCommits = prCommitCount + directCommits.length;
  lines.push("## Summary", "");
  lines.push(
    `- ${plural(pullRequests.length, "pull request")} and ${plural(totalCommits, "commit")}` +
      (previousTag ? ` since [\`${previousTag}\`](${repoUrl}/releases/tag/${previousTag})` : " in this first release"),
  );
  const contributors = uniqueContributors(pullRequests, directCommits);
  if (contributors.length > 0) {
    lines.push(`- Contributors: ${contributors.join(", ")}`);
  }
  lines.push("");

  if (pullRequests.length > 0) {
    lines.push("## Pull requests", "");
    for (const pr of pullRequests) {
      const byline = pr.author ? ` by @${pr.author}` : "";
      lines.push(`### ${escapeMarkdown(pr.title)} ([#${pr.number}](${pr.url}))${byline}`, "");

      if (pr.labels.length > 0) {
        lines.push(`Labels: ${pr.labels.map((label) => `\`${label}\``).join(", ")}`, "");
      }

      if (pr.body) {
        lines.push("<details>", "<summary>Description</summary>", "");
        lines.push(truncate(pr.body, MAX_PR_BODY_LENGTH), "");
        lines.push("</details>", "");
      }

      lines.push(`**${plural(pr.commits.length, "commit")}**`, "");
      for (const commit of pr.commits) {
        lines.push(`- [\`${commit.shortSha}\`](${repoUrl}/commit/${commit.sha}) ${escapeMarkdown(commit.subject)}`);
      }
      lines.push("");
    }
  }

  if (directCommits.length > 0) {
    lines.push("## Other commits", "");
    lines.push("Changes pushed without a pull request.", "");
    for (const commit of directCommits) {
      lines.push(
        `- [\`${commit.shortSha}\`](${repoUrl}/commit/${commit.sha}) ${escapeMarkdown(commit.subject)} (${commit.author})`,
      );
    }
    lines.push("");
  }

  if (pullRequests.length === 0 && directCommits.length === 0) {
    lines.push("_No changes since the previous tag._", "");
  }

  if (previousTag) {
    lines.push(`**Full changelog:** [\`${previousTag}...${tag}\`](${repoUrl}/compare/${previousTag}...${tag})`);
  } else {
    lines.push(`**Full changelog:** [\`${tag}\`](${repoUrl}/commits/${tag})`);
  }

  return lines.join("\n").trim() + "\n";
}

async function upsertRelease(github, owner, repo, tag, body, core) {
  const prerelease = /-/.test(tag);
  let existing = null;
  try {
    existing = (await github.rest.repos.getReleaseByTag({ owner, repo, tag })).data;
  } catch (error) {
    if (error.status !== 404) throw error;
  }

  if (existing) {
    core.info(`Updating existing release ${existing.id} for ${tag}`);
    const { data } = await github.rest.repos.updateRelease({
      owner,
      repo,
      release_id: existing.id,
      name: tag,
      body,
      prerelease,
      draft: false,
      make_latest: prerelease ? "false" : "true",
    });
    return data;
  }

  core.info(`Creating release for ${tag}`);
  const { data } = await github.rest.repos.createRelease({
    owner,
    repo,
    tag_name: tag,
    name: tag,
    body,
    prerelease,
    draft: false,
    make_latest: prerelease ? "false" : "true",
  });
  return data;
}

function uniqueContributors(pullRequests, directCommits) {
  const names = new Set();
  for (const pr of pullRequests) {
    if (pr.author) names.add(`@${pr.author}`);
  }
  for (const commit of directCommits) {
    if (commit.author) names.add(commit.author);
  }
  return [...names];
}

function cleanSubject(subject) {
  // Strip stray bold markers and surrounding whitespace some commit tools leave behind.
  return subject.replace(/^\*\*(.*)\*\*$/, "$1").trim();
}

function escapeMarkdown(text) {
  // Keep PR/commit titles from being interpreted as Markdown structure.
  return text.replace(/([<>])/g, "\\$1");
}

function plural(count, noun) {
  return `${count} ${noun}${count === 1 ? "" : "s"}`;
}

function truncate(text, max) {
  if (text.length <= max) return text;
  return `${text.slice(0, max - 20).trimEnd()}\n\n_… truncated_`;
}
