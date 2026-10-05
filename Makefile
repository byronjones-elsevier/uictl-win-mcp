# Makefile for uictl-win-mcp. Run `make` for the target list.

SHELL := /bin/sh

CONFIG   ?= Release
SOLUTION ?= uictl.slnx
CLI_PROJ ?= src/UICtl.Cli/UICtl.Cli.csproj
TFM      ?= net10.0-windows10.0.19041.0
DOTNET   ?= dotnet
UICTL    ?= src/UICtl.Cli/bin/$(CONFIG)/$(TFM)/uictl.exe
PUBLISH_DIR ?= artifacts/publish
RID      ?= win-x64

.DEFAULT_GOAL := help

.PHONY: help default all deps build rebuild test format format/check run daemon/stop publish install clean check/dotnet

## This help screen
help:
	@printf "Available targets:\n\n"
	@awk '/^[a-zA-Z\-_0-9%:\/]+/ { \
		helpMessage = match(lastLine, /^## (.*)/); \
		if (helpMessage) { \
			helpCommand = $$1; \
			helpMessage = substr(lastLine, RSTART + 3, RLENGTH); \
			gsub("\\\\", "", helpCommand); \
			gsub(":+$$", "", helpCommand); \
			printf "  \x1b[32;01m%-35s\x1b[0m %s\n", helpCommand, helpMessage; \
		} \
	} \
	{ lastLine = $$0 }' $(MAKEFILE_LIST) | sort -u
	@printf "\n"

## Verify the .NET SDK is installed
check/dotnet:
	@command -v $(DOTNET) >/dev/null 2>&1 || { echo "error: $(DOTNET) not found; install the .NET 10 SDK" >&2; exit 1; }

## Restore NuGet packages
deps: check/dotnet
	$(DOTNET) restore $(SOLUTION)

## Build the solution (CONFIG=Release|Debug)
build: deps
	$(DOTNET) build $(SOLUTION) -c $(CONFIG) --no-restore

## Clean and rebuild from scratch
rebuild: clean build

## Run the xunit tests (needs real Windows; see TESTING.md)
test: build
	$(DOTNET) test $(SOLUTION) -c $(CONFIG) --no-build

## Apply dotnet format
format: check/dotnet
	$(DOTNET) format $(SOLUTION)

## Fail if code is not formatted
format/check: check/dotnet
	$(DOTNET) format $(SOLUTION) --verify-no-changes

## Run the CLI; pass ARGS="windows --app Notepad"
run: build
	$(DOTNET) run --project $(CLI_PROJ) -c $(CONFIG) --no-build -- $(ARGS)

## Stop the cached uictl daemon (do this after rebuilding)
daemon/stop:
	-$(UICTL) daemon stop

## Publish a self-contained single-file uictl.exe to PUBLISH_DIR
publish: deps
	$(DOTNET) publish $(CLI_PROJ) -c $(CONFIG) -r $(RID) --self-contained -p:PublishSingleFile=true -o $(PUBLISH_DIR)

## Register the built uictl with Claude Code as an MCP server
install: build
	claude mcp add uictl -- "$(abspath $(UICTL))" mcp

## Remove build outputs
clean: check/dotnet
	$(DOTNET) clean $(SOLUTION) -c $(CONFIG) --nologo -v q
	rm -rf $(PUBLISH_DIR)

## Build (default workflow)
default: build

## Build and test
all: build test
