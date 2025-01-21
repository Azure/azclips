# Detect OS
ifeq ($(OS),Windows_NT)
    DETECTED_OS := Windows
    EXE_EXT := .exe
    # Use PowerShell commands for Windows
    MKDIR_CMD := powershell mkdir -Force
else
    DETECTED_OS := $(shell uname -s)
    EXE_EXT :=
    # Use Unix commands for Linux/MacOS
    MKDIR_CMD := mkdir -p
endif

AZ_DIR=./az
OUTPUT_DIR=./build
MODULES_DIR=./modules

# Get just the module names without the full path
MODULE_NAMES=$(notdir $(wildcard $(MODULES_DIR)/*))

# Create output directories if they don't exist
$(OUTPUT_DIR):
	$(MKDIR_CMD) "$(OUTPUT_DIR)"

$(OUTPUT_DIR)/modules: | $(OUTPUT_DIR)
	$(MKDIR_CMD) "$(OUTPUT_DIR)/modules"

build-modules: clean-processes $(OUTPUT_DIR)/modules $(MODULE_NAMES)

$(MODULE_NAMES):
	go build -o "$(OUTPUT_DIR)/modules/$@.azm" "$(MODULES_DIR)/$@/main.go"

build-az: $(AZ_DIR)
	go build -o "$(OUTPUT_DIR)/az$(EXE_EXT)" "$(AZ_DIR)/main.go"

build: clean-processes build-proto build-modules build-az

build-proto:
	buf generate

run: build
	ifeq ($(DETECTED_OS),Windows)
		cd $(OUTPUT_DIR) && powershell -Command "$$env:AZURE_CLI_MODULE_PATH='$(OUTPUT_DIR)/modules'; ./az$(EXE_EXT)"
	else
		cd $(OUTPUT_DIR) && AZURE_CLI_MODULE_PATH="$(OUTPUT_DIR)/modules" ./az$(EXE_EXT)
	endif

clean-processes:
ifeq ($(DETECTED_OS),Windows)
	@powershell -Command "Get-Process | Where-Object {$$_.Name -like '*.azm'} | Stop-Process -Force -ErrorAction SilentlyContinue"
else
	@pkill -f "az|.*\.azm" || true
endif

clean:
	go clean
	rm -rf $(OUTPUT_DIR)

.PHONY: build build-modules build-az clean
