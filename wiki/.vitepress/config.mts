import { withMermaid } from 'vitepress-plugin-mermaid'

export default withMermaid({
  title: 'Azclips Wiki',
  description: 'Technical documentation for Project Azclips — a .NET 10 extension-based CLI (the `az` binary) for Azure resource management, with an out-of-process AI agent fallback, an interactive Terminal UI, and mirrored PowerShell cmdlets',
  appearance: 'force-dark',
  ignoreDeadLinks: true,

  head: [
    ['link', { rel: 'icon', href: '/logo.svg' }],
    ['link', { rel: 'preconnect', href: 'https://fonts.googleapis.com' }],
    ['link', { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' }],
    ['link', { href: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap', rel: 'stylesheet' }],
  ],

  markdown: {
    lineNumbers: true,
  },

  themeConfig: {
    logo: '/logo.svg',
    siteTitle: 'Azclips Wiki',

    outline: { level: [2, 3] },

    nav: [
      { text: 'Home', link: '/' },
      { text: 'Onboarding', link: '/onboarding-guide' },
      { text: 'Getting Started', link: '/01-getting-started/overview' },
      { text: 'Deep Dive', link: '/02-deep-dive/core-engine-orchestration' },
    ],

    sidebar: [
      {
        text: '🚀 Onboarding',
        collapsed: false,
        items: [
          { text: 'Principal-Level Guide', link: '/onboarding-guide' },
          { text: 'Zero to Hero', link: '/zero-to-hero-guide' },
        ],
      },
      {
        text: '📖 Getting Started',
        collapsed: false,
        items: [
          { text: 'Overview & Architecture', link: '/01-getting-started/overview' },
          { text: 'Environment Setup', link: '/01-getting-started/environment-setup' },
          { text: 'CLI Usage', link: '/01-getting-started/cli-usage' },
          { text: 'PowerShell Module Usage', link: '/01-getting-started/powershell-usage' },
          { text: 'Terminal UI Quickstart', link: '/01-getting-started/tui-quickstart' },
          { text: 'AI Agent Fallback Quickstart', link: '/01-getting-started/agent-mode-quickstart' },
        ],
      },
      {
        text: '🔬 Deep Dive',
        collapsed: false,
        items: [
          {
            text: 'Agent, Copilot SDK & Interactive Surfaces',
            collapsed: false,
            items: [
              { text: 'Agent Architecture', link: '/02-deep-dive/agent-architecture' },
              { text: 'Copilot SDK Integration', link: '/02-deep-dive/copilot-sdk-integration' },
              { text: 'Agent Tools', link: '/02-deep-dive/agent-tools' },
              { text: 'Prompts & Plan Mode', link: '/02-deep-dive/prompts-and-plan-mode' },
              { text: 'Rendering & Terminal UX', link: '/02-deep-dive/rendering-and-terminal-ux' },
              { text: 'TUI Agent Mode & Fallback Interplay', link: '/02-deep-dive/tui-agent-mode' },
              { text: 'TUI Interactive Shell & Azure VFS', link: '/02-deep-dive/tui-interactive-shell' },
            ],
          },
          {
            text: 'Core Runtime, Auth & Extensibility',
            collapsed: false,
            items: [
              { text: 'Core Engine & Orchestration', link: '/02-deep-dive/core-engine-orchestration' },
              { text: 'HTTP Pipeline & Runtime', link: '/02-deep-dive/http-pipeline-runtime' },
              { text: 'Authentication (MSAL)', link: '/02-deep-dive/authentication' },
              { text: 'Extension System', link: '/02-deep-dive/extension-system' },
              { text: 'PowerShell Cmdlet Architecture', link: '/02-deep-dive/powershell-cmdlet-architecture' },
            ],
          },
          {
            text: 'Platform: Configuration, Telemetry, Packaging & Quality',
            collapsed: false,
            items: [
              { text: 'Configuration Management', link: '/02-deep-dive/configuration-management' },
              { text: 'Telemetry & Observability', link: '/02-deep-dive/telemetry-observability' },
              { text: 'Packaging & Distribution', link: '/02-deep-dive/packaging-and-distribution' },
              { text: 'Testing & Build Pipeline', link: '/02-deep-dive/testing-and-build' },
            ],
          },
        ],
      },
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/Azure/azclips' },
    ],

    search: {
      provider: 'local',
    },

    footer: {
      message: 'Generated with Deep Wiki',
      copyright: '© 2025 Microsoft Corporation',
    },
  },

  mermaid: {
    theme: 'dark',
    themeVariables: {
      darkMode: true,
      background: '#0d1117',
      primaryColor: '#2d333b',
      primaryTextColor: '#e6edf3',
      primaryBorderColor: '#6d5dfc',
      secondaryColor: '#1c2333',
      secondaryTextColor: '#e6edf3',
      secondaryBorderColor: '#6d5dfc',
      tertiaryColor: '#161b22',
      tertiaryTextColor: '#e6edf3',
      tertiaryBorderColor: '#30363d',
      lineColor: '#8b949e',
      textColor: '#e6edf3',
      mainBkg: '#2d333b',
      nodeBkg: '#2d333b',
      nodeBorder: '#6d5dfc',
      nodeTextColor: '#e6edf3',
      clusterBkg: '#161b22',
      clusterBorder: '#30363d',
      titleColor: '#e6edf3',
      edgeLabelBackground: '#1c2333',
      actorBkg: '#2d333b',
      actorTextColor: '#e6edf3',
      actorBorder: '#6d5dfc',
      actorLineColor: '#8b949e',
      signalColor: '#e6edf3',
      signalTextColor: '#e6edf3',
      labelBoxBkgColor: '#2d333b',
      labelBoxBorderColor: '#6d5dfc',
      labelTextColor: '#e6edf3',
      loopTextColor: '#e6edf3',
      activationBorderColor: '#6d5dfc',
      activationBkgColor: '#1c2333',
      sequenceNumberColor: '#e6edf3',
      noteBkgColor: '#2d333b',
      noteTextColor: '#e6edf3',
      noteBorderColor: '#6d5dfc',
      classText: '#e6edf3',
      labelColor: '#e6edf3',
      altBackground: '#161b22',
    },
  },

  vite: {
    optimizeDeps: {
      include: ['mermaid'],
    },
  },
})
