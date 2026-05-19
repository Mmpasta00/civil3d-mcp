# Civil 3D MCP — Mohammad's Setup Notes

> This is Mohammad's customized fork of `barbosaihan/civil3d-mcp`.
> Upstream README is `README.md`. This file is just our pivot notes.

## What This Is (Plain English)
A plugin that lets you chat with Claude (or any AI assistant) and have it actually **do things** inside Civil 3D — read drawings, run calcs, edit pipes, import surfaces. The AI writes code on the fly, the plugin runs it inside Civil 3D, results come back to the chat.

## What's Different From Upstream

| Change | Reason |
|---|---|
| Target framework: `net8.0-windows` → `net10.0-windows` | Civil 3D 2026 runs on .NET 10 |
| Reference DLLs: local `C_References/` → `Speckle.Civil3D.API 2026.0.0` NuGet | Build on Mac without copying DLLs from work PC |
| 9 new skills in `skills/utility_optimization/`, `skills/hydrology/`, `skills/qa/` | Match Mohammad's actual workflows |
| `setup-work-pc.ps1` and `claude-desktop-config.example.json` | One-shot setup on the work PC |

## Custom Skills Added (your workflows)

### Utility Optimization
| Skill | What it does |
|---|---|
| `list_pipe_networks` | All gravity networks + pipe/structure counts |
| `export_pipe_network` | Full pipe-level CSV-equivalent JSON — feeds O-Pipes pipeline |
| `check_pipe_cover` | Find pipes below min cover against a surface |
| `find_pipe_conflicts` | Detect vertical utility crossings below clearance threshold |
| `resize_pipe` | Swap a pipe's part size (writes to drawing) |

### Hydrology
| Skill | What it does |
|---|---|
| `rational_peak_flow` | Q = C × i × A for one or many catchments |
| `composite_runoff_coefficient` | Weighted-average C for mixed-cover catchment |

### Surfaces
| Skill | What it does |
|---|---|
| `import_landxml_surface` | Bulk-import LandXML files — unblocks Surface Creation workflow |

### QA
| Skill | What it does |
|---|---|
| `drawing_health_check` | Multi-point scan: counts, xref status, layers |
| `layer_compliance` | Check layer names against firm prefix standard |

## Day-1 Setup on Work PC

```powershell
# 1. Open PowerShell as a normal user
# 2. cd to wherever you want the repo
cd $HOME\Documents
# 3. Clone
git clone https://github.com/Mmpasta00/civil3d-mcp.git
cd civil3d-mcp
# 4. Run the one-shot setup
powershell -ExecutionPolicy Bypass -File .\setup-work-pc.ps1
```

The script verifies Git, Node.js, and .NET 10 SDK are installed (prompts you to install whichever is missing), then builds both halves.

## Then in Civil 3D

1. Open Civil 3D 2026.
2. Type `NETLOAD` → browse to the DLL the script printed (something like `Documents\civil3d-mcp\plugin\Civil3dMcpPlugin\bin\Debug\net10.0-windows\Civil3dMcpPlugin.dll`).
3. Type `C3DMCPSTATUS` → should say "listening on port 8080".

## Then in Claude Desktop

1. Open `%APPDATA%\Claude\claude_desktop_config.json` (create if it doesn't exist).
2. Paste the contents of `claude-desktop-config.example.json` from this repo.
3. Replace `YOUR_USERNAME` with your Windows username.
4. Restart Claude Desktop.

## First Real Test
With a Civil 3D drawing open and the plugin loaded, ask Claude:

> *"Run a drawing health check and tell me what's in this DWG."*

Claude should call `civil3d_skills` to find `drawing_health_check`, then `civil3d_query` to run it, then summarize the results in chat.

## Build From Mac (Reminder)
The plugin builds clean on Mac too — useful for editing skills, fixing bugs, adding tools. Loop:

```bash
cd "Civil 3D Plugin/mcp-server"
# Edit skill files in skills/ or C# files in plugin/Civil3dMcpPlugin/
npm run build                                              # rebuild TS server
cd plugin/Civil3dMcpPlugin && dotnet build                 # rebuild C# plugin
cd ../.. && git add . && git commit -m "msg" && git push   # ship to work PC
```

Then on the work PC: `git pull` → `NETLOAD` the new `Civil3dMcpPlugin.dll` → test.
