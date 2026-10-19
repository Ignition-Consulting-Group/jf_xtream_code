SLN := Jellyfin.Plugin.XtreamPro.slnx
PLUGIN := src/Jellyfin.Plugin.XtreamPro
PLUGIN_OUT := artifacts/plugin
COMPOSE := docker compose -f docker/docker-compose.dev.yml
GATED := Api Sync LiveTv CatchUp
COVERAGE_ARGS := --coverlet --coverlet-output-format cobertura --coverlet-threshold 80 --coverlet-threshold-stat Total \
	$(foreach ns,$(GATED),--coverlet-include "[Jellyfin.Plugin.XtreamPro]Jellyfin.Plugin.XtreamPro.$(ns).*")

.PHONY: build test coverage test-int test-ui screenshots bench format dev-up dev-down dev-reload release-dry

build:
	dotnet build $(SLN) -c Release

test:
	dotnet test --project tests/XtreamPro.UnitTests

coverage:
	dotnet test --project tests/XtreamPro.UnitTests -c Release $(COVERAGE_ARGS)

test-int:
	dotnet test --project tests/XtreamPro.IntegrationTests

test-ui:
	dotnet test --project tests/XtreamPro.UiTests --filter-not-trait "Category=Screenshots"

screenshots:
	dotnet test --project tests/XtreamPro.UiTests --filter-trait "Category=Screenshots"

bench:
	dotnet run -c Release --project benchmarks/XtreamPro.Benchmarks -- --filter '*'

format:
	dotnet format $(SLN)

$(PLUGIN_OUT):
	dotnet publish $(PLUGIN) -c Release -o $(PLUGIN_OUT)

dev-up: $(PLUGIN_OUT)
	$(COMPOSE) up -d --build

dev-down:
	$(COMPOSE) down

dev-reload:
	rm -rf $(PLUGIN_OUT)
	$(MAKE) $(PLUGIN_OUT)
	$(COMPOSE) restart jellyfin
	$(COMPOSE) logs -f jellyfin

release-dry:
	mkdir -p $(CURDIR)/artifacts/release
	jprm plugin build $(PLUGIN) --output $(CURDIR)/artifacts/release --version 0.0.0
	sha256sum artifacts/release/*.zip
