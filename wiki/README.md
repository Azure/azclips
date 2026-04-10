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

## Deployment

The wiki is automatically deployed to GitHub Pages via GitHub Actions.

**Live Site**: https://animated-adventure-r3wzl2j.pages.github.io/

Deployment triggers on push to `main` branch when wiki files change. No manual setup required.

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
└── package.json            # Dependencies
```

## Tech Stack

- [VitePress](https://vitepress.dev/) - Static site generator
- [Mermaid](https://mermaid.js.org/) - Diagrams and flowcharts
- [Medium Zoom](https://github.com/francoischalifour/medium-zoom) - Image zoom
- GitHub Pages - Hosting
