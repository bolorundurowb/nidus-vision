#!/bin/sh
# Starts as root only long enough to hand the data volumes to the unprivileged
# `app` user, then replaces this shell with the server. A later boot skips the
# walk when the directory is already owned by that user and the marker agrees.
set -eu

APP_USER=app

if [ "$(id -u)" != "0" ]; then
  echo "nidus: entrypoint is not root, so volume ownership will not be fixed" >&2
  exec "$@"
fi

if ! id "$APP_USER" >/dev/null 2>&1; then
  echo "nidus: user $APP_USER does not exist in this image" >&2
  exit 1
fi

app_uid=$(id -u "$APP_USER")
app_gid=$(id -g "$APP_USER")
expected="$app_uid:$app_gid"

# Refuse paths that would re-own the OS or the published application.
unsafe_dir() {
  case "$1" in
    / | /app | /bin | /sbin | /lib | /lib64 | /usr | /etc | /proc | /sys | /dev | /root | /boot | /var | /run | /tmp | /home)
      return 0
      ;;
    /bin/* | /sbin/* | /lib/* | /lib64/* | /usr/* | /etc/* | /proc/* | /sys/* | /dev/* | /root/* | /boot/* | /run/*)
      return 0
      ;;
  esac
  return 1
}

prepare_dir() {
  raw=$1
  [ -n "$raw" ] || return 0

  # -m allows the directory to be missing. -f runs again after mkdir so a
  # symlink created in between cannot point the walk at the filesystem root.
  candidate=$(readlink -m "$raw") || {
    echo "nidus: cannot resolve $raw" >&2
    exit 1
  }
  if unsafe_dir "$candidate"; then
    echo "nidus: refusing to change ownership of $candidate" >&2
    exit 1
  fi

  mkdir -p "$candidate"
  dir=$(readlink -f "$candidate") || {
    echo "nidus: cannot resolve $candidate" >&2
    exit 1
  }
  if unsafe_dir "$dir"; then
    echo "nidus: refusing to change ownership of $dir" >&2
    exit 1
  fi
  marker="$dir/.nidus-owner"
  owner=$(stat -c '%u:%g' "$dir")
  recorded=
  if [ -f "$marker" ]; then
    recorded=$(cat "$marker" 2>/dev/null || true)
  fi
  if [ "$owner" = "$expected" ] && [ "$recorded" = "$expected" ]; then
    return 0
  fi

  echo "nidus: giving $dir to $APP_USER ($expected); a large volume can take several minutes" >&2
  # -xdev stays on this mount, so a nested mount is not re-owned by accident.
  find "$dir" -xdev -exec chown "$expected" {} +
  printf '%s\n' "$expected" >"$marker"
  chown "$expected" "$marker"
}

prepare_dir "${Storage__DataDirectory:-/app/data}"
prepare_dir "${Storage__RecordingsDirectory:-/var/nidus/recordings}"
prepare_dir "${Storage__EventsDirectory:-/var/nidus/events}"

# Keep groups granted by `group_add`, and add whoever owns the GPU nodes.
# Never keep group 0: that is root's group, not a device group we should join.
groups=$app_gid
add_group() {
  gid=$1
  [ -n "$gid" ] || return 0
  [ "$gid" = "0" ] && return 0
  case ",$groups," in
    *",$gid,"*) return 0 ;;
  esac
  groups="$groups,$gid"
}

for gid in $(id -G); do
  add_group "$gid"
done

if [ -d /dev/dri ]; then
  for node in /dev/dri/*; do
    [ -e "$node" ] || continue
    add_group "$(stat -c '%g' "$node")"
  done
fi

home=$(getent passwd "$APP_USER" | cut -d: -f6)
if [ -z "$home" ] || [ ! -d "$home" ] || [ "$(stat -c '%u' "$home")" != "$app_uid" ]; then
  home=/tmp
fi

exec setpriv \
  --reuid="$app_uid" \
  --regid="$app_gid" \
  --groups="$groups" \
  --inh-caps=-all \
  --bounding-set=-all \
  --no-new-privs \
  -- env HOME="$home" "$@"
