#!/bin/sh
set -eu

awslocal s3api create-bucket \
  --bucket "${STORAGE_BUCKET:-agent-artifacts}" \
  --region eu-west-1 \
  --create-bucket-configuration LocationConstraint=eu-west-1
