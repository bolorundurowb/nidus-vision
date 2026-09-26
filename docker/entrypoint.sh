#!/bin/sh
# Starts as root only to hand the data and recordings volumes to the unprivileged
# `app` user, then execs the server as that user. Volumes written by images that
# ran as root are chowned once; the marker keeps later boots from walking the
# recordings tree again.
set -eu

APP_USER=app

if [ "$(id -u)" != "0" ]; then
  exec "$@"
fi

for dir in "${Storage__DataDirectory:-/app/data}" \
           "${Storage__RecordingsDirectory:-/var/nidus/recordings}" \
           "${Storage__EventsDirectory:-}"; do
  [ -n "$dir" ] || continue
  mkdir -p "$dir"
  marker="$dir/.nidus-owner"
  if [ ! -f "$marker" ]; then
    echo "nidus: giving $dir to $APP_USER (first start after the non-root change can take a while on a large volume)"
    chown -R "$APP_USER:$APP_USER" "$dir"
    touch "$marker"
    chown "$APP_USER:$APP_USER" "$marker"
  fi
done

# Keep access to the Intel GPU nodes that compose maps in, whatever GIDs the host uses.
groups="$(id -g "$APP_USER")"
for node in /dev/dri/*; do
  [ -e "$node" ] || continue
  gid="$(stat -c %g "$node")"
  case ",$groups," in
    *",$gid,"*) ;;
    *) groups="$groups,$gid" ;;
  esac
done

home="$(getent passwd "$APP_USER" | cut -d: -f6)"
exec setpriv --reuid="$APP_USER" --regid="$APP_USER" --groups="$groups" --inh-caps=-all \
  -- env HOME="${home:-/tmp}" "$@"
