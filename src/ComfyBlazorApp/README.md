# ComfyBlazorApp

A self-hosted **Blazor Server** web UI for [ComfyUI](https://github.com/comfyanonymous/ComfyUI) — giving you a clean, persistent interface to generate and edit images, manage reusable prompts, and run multi-prompt batch jobs.

---

## Features

| Feature | Description |
|---|---|
| **Generate & Edit** | Queue image generation or edit an existing image via ComfyUI workflows |
| **Prompt Bank** | Save, tag, and reuse prompt presets with optional thumbnail previews |
| **Prompt History** | Every run is recorded — re-run or edit-and-re-run any past generation |
| **Image Library** | Upload source images once, reuse them across any number of Edit jobs |
| **Edit Queue** | Pick one source image + multiple prompts → sequential batch edit jobs |
| **Batch Builder** | Queue multiple Generate presets as a single batch job |
| **Batch Monitor** | Real-time job status, progress bars, and direct links to output files |
| **Gallery** | Browse generated outputs with a fullscreen lightbox viewer |

---

## Prerequisites

| Requirement | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 or later |
| [ComfyUI](https://github.com/comfyanonymous/ComfyUI) | Running locally (default `http://127.0.0.1:8188`) |

---

## Quick Start

### Windows

```bat
build.bat
dotnet run
```

### Linux / macOS

```bash
chmod +x build.sh
./build.sh
dotnet run
```

The build script will:
1. Install the **LibMan CLI** tool globally (`dotnet tool install -g Microsoft.Web.LibraryManager.Cli`)
2. Run `libman restore` to download Bootstrap, Bootstrap Icons, and PhotoSwipe into `wwwroot/lib/`
3. Run `dotnet restore` for NuGet packages
4. Build the app in **Release** mode

---

## Configuration

Edit `appsettings.json` to match your ComfyUI setup:

```json
{
  "ComfyUI": {
    "BaseUrl": "http://127.0.0.1:8188",
    "OutputDirectory": "ComfyUI-output",
    "GenerateWorkflowTemplatePath": "Data/workflow-generate-template.json",
    "EditWorkflowTemplatePath": "Data/workflow-edit-template.json"
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `BaseUrl` | `http://127.0.0.1:8188` | ComfyUI API base URL |
| `OutputDirectory` | `ComfyUI-output` | Path to the ComfyUI output folder (relative or absolute) |
| `GenerateWorkflowTemplatePath` | `Data/workflow-generate-template.json` | ComfyUI API JSON workflow for generation |
| `EditWorkflowTemplatePath` | `Data/workflow-edit-template.json` | ComfyUI API JSON workflow for image editing |

---

## Data & Storage

All runtime data is stored locally and **excluded from git**:

| Path | Contents |
|---|---|
| `data/prompts.db` | SQLite database (presets, batches, history, source images) |
| `data/prompt-images/` | Preset thumbnail images |
| `data/source-images/` | Image Library uploads |

The database schema is managed by **EF Core migrations** and applied automatically on startup — no manual `dotnet ef` commands needed to run the app.

---

## Project Structure

```
ComfyBlazorApp/
├── Components/
│   ├── Layout/          # MainLayout, NavMenu
│   └── Pages/           # Home, Gallery, Prompts, EditQueue,
│                        # ImageLibrary, Batch, BatchMonitor, History
├── Data/                # EF Core entities + AppDbContext
├── Migrations/          # EF Core migrations (committed to source control)
├── Models/              # Request/response models
├── Options/             # Strongly-typed config classes
├── Services/            # ComfyUiService, BatchWorker, PromptService, etc.
├── wwwroot/             # Static assets (CSS, images)
│   └── lib/             # Client libraries (restored by libman — gitignored)
├── appsettings.json     # App configuration
├── libman.json          # Client-side library manifest
├── build.bat            # Windows build script
└── build.sh             # Linux/macOS build script
```

---

## Client-Side Libraries

Managed via [LibMan](https://learn.microsoft.com/en-us/aspnet/core/client-side/libman/) (`libman.json`):

| Library | Version | Purpose |
|---|---|---|
| Bootstrap | 5.3.3 | Base CSS utilities |
| Bootstrap Icons | 1.13.1 | Navigation and UI icons |
| PhotoSwipe | 5.4.4 | Fullscreen image lightbox in Gallery |

> The `wwwroot/lib/` directory is **gitignored** — run `libman restore` (or the build script) to restore them.

---

## Development

```bash
dotnet run
# App available at https://localhost:5001 (or http://localhost:5000)
```

Hot reload is supported via `dotnet watch`:

```bash
dotnet watch
```
