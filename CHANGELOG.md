# Changelog

Release notes for tagged images (`bolorundurowb/nidus-vision`). The publish workflow pushes a tag and `latest` only after the build and tests pass. Dates are the tag date.

## Unreleased

- Camera password encryption keys are stored in the data directory (`keys/`, inside the `nidus-data` volume) so they survive a container recreate. Installs that saved camera passwords before this change need those passwords entered again after upgrading, because the previous keys lived only in the container filesystem.
- README covers codec expectations, disk sizing, backup, upgrades, and the LAN-only network default.
