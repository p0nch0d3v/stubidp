.DEFAULT_GOAL := help
.PHONY: help dotnet sql sqlup sqldown sqllog

BASH_BIN := $(shell command -v bash 2>/dev/null || which bash 2>/dev/null || printf '%s' /bin/bash)

help: ## Display this help
	@echo "Available targets:"
	@awk -F ':.*##' '/^[^ ]+:.*##/ { printf "  \033[36m%-15s\033[0m %s\n", $$1, $$2 }' $(MAKEFILE_LIST)

devcontainer: ## Build the Dev Container Image
	docker build \
		--progress=plain \
		--no-cache \
		--debug \
		--load \
		--file .devcontainer/Dockerfile \
		--tag stubidp-devcontainer-base:latest \
		.
