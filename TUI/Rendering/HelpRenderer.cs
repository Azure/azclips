using Hex1b;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Core.Tui.Rendering;

static class HelpRenderer
{
    private static readonly string HelpMarkdown = """
        # Cirrus Help

        ## Basics

        | Symbol | Description |
        |--------|-------------|
        | `@` | Add file context (e.g. @src/myFile.ts) |
        | `!` | Execute shell command (e.g. !npm run start) |

        ## Commands

        | Command | Description |
        |---------|-------------|
        | `login` | Login to Azure |
        | `logout` | Logout from Azure |
        | `profile show --active` | Show the active profile |
        | `group list` | List resource groups |
        | `group create` | Create a resource group |
        | `extension add` | Add an extension |

        ## Keyboard Shortcuts

        | Key | Action |
        |-----|--------|
        | **Enter** | Execute command |
        | **Tab** | Accept suggestion |
        | **↑ / ↓** | Browse suggestions / history |
        | **PgUp / PgDn** | Scroll output |
        | **Ctrl+V** | Paste from clipboard |
        | **Ctrl+L** | Clear output |
        | **F1** | Toggle this help screen |
        | **F2** | Toggle Azure / Local mode |
        | **Esc** | Close help / cancel |

        ## Clipboard

        - `/copy [n]` — Copy output to clipboard
        - `Shift+drag` — Select text in terminal
        - `Ctrl+C` — Copy selected text
        - `Ctrl+V` — Paste from clipboard

        ## Tips

        - Type a natural language query for AI help
        - Use tab completion for faster input
        - Commands are forwarded to az CLI

        ---

        Press **Esc** or **F1** to close.
        """;

    public static Hex1bWidget Render(WidgetContext<VStackWidget> v)
    {
        return v.VScrollPanel(sp => [
            sp.Markdown(HelpMarkdown),
        ]).Fill();
    }
}
