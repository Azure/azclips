# Cirrus Wiki

VitePress-based documentation site for the Cirrus CLI framework.

## Local Development

```bash
# Install dependencies
npm install

# Start dev server (with hot reload)
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview
```

## Automated Deployment to Azure Storage

The wiki is automatically deployed to Azure Storage as a static website via GitHub Actions.

### Initial Setup

1. **Run the setup script** (one-time setup):
   ```bash
   cd wiki
   ./setup-deployment.sh
   ```

   This script will:
   - Check for existing Azure service principal credentials in `.env`
   - Create a new service principal if needed (requires Azure CLI login)
   - Prompt you for a storage account name if not already configured
   - Upload `STORAGE_CREDENTIALS` and `CIRRUS_WEB_STORAGE` secrets to GitHub
   - Save configuration to `.env` file (git-ignored)

2. **Push to trigger deployment**:
   ```bash
   git push origin main
   ```

   The [deploy-wiki.yml](../.github/workflows/deploy-wiki.yml) workflow will:
   - Build the wiki using VitePress
   - Create the storage account if it doesn't exist
   - Deploy the built site to Azure Storage static website hosting
   - Output the public URL

### Manual Configuration

If you prefer to configure manually:

1. Create `.env` from the example:
   ```bash
   cp .env.example .env
   ```

2. Add your Azure service principal credentials and storage account name to `.env`

3. Set GitHub secrets manually:
   ```bash
   gh secret set STORAGE_CREDENTIALS --repo <owner/repo>
   gh secret set CIRRUS_WEB_STORAGE --repo <owner/repo>
   ```

## Project Structure

```
wiki/
├── .vitepress/
│   ├── config.mts          # VitePress configuration
│   ├── theme/              # Custom theme files
│   └── dist/               # Build output (generated)
├── public/                 # Static assets
├── 01-getting-started/     # Getting started guides
├── 02-deep-dive/           # Deep dive documentation
├── index.md                # Home page
├── package.json            # Dependencies
├── setup-deployment.sh     # Deployment setup script
└── .env.example            # Environment variables template
```

## Tech Stack

- [VitePress](https://vitepress.dev/) - Static site generator
- [Mermaid](https://mermaid.js.org/) - Diagrams and flowcharts
- [Medium Zoom](https://github.com/francoischalifour/medium-zoom) - Image zoom
- Azure Storage Static Websites - Hosting
