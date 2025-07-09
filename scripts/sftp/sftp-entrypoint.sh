#!/bin/bash
set -e

# Fix permissions after user and folders are created
chmod -R 777 /home/${SFTP_USER}/upload || true

# Call the original entrypoint
/entrypoint "$@"