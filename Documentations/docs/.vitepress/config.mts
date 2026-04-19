import { defineConfig } from 'vitepress'

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: "CaeriusNet",
  description: "High-performance .NET 10 / C# 14 micro-ORM for SQL Server Stored Procedures",

  head: [
    ['link', { rel: 'icon', type: 'image/png', href: '/logo.png' }],
    ['meta', { name: 'theme-color', content: '#0078d4' }]
  ],

  themeConfig: {
    logo: 'logo.png',
    siteTitle: 'CaeriusNet',

    nav: [
      { text: 'Home', link: '/' },
      { text: 'Get Started', link: '/quickstart/what-is-caeriusnet' },
      {
        text: 'Guides',
        items: [
          { text: 'Reading Data', link: '/documentation/reading-data' },
          { text: 'Writing Data', link: '/documentation/writing-data' },
          { text: 'Table-Valued Parameters', link: '/documentation/tvp' },
          { text: 'Multiple Result Sets', link: '/documentation/multi-results' },
          { text: 'Caching', link: '/documentation/cache' },
          { text: 'Aspire Integration', link: '/documentation/aspire' },
        ]
      },
      {
        text: 'Reference',
        items: [
          { text: 'API Reference', link: '/documentation/api' },
          { text: 'Best Practices', link: '/documentation/best-practices' },
        ]
      },
      { text: 'Performance', link: '/benchmarks/' }
    ],

    sidebar: [
      {
        text: 'Get Started',
        collapsed: false,
        items: [
          { text: 'What is CaeriusNet?', link: '/quickstart/what-is-caeriusnet' },
          { text: 'Installation & Setup', link: '/quickstart/getting-started' }
        ]
      },
      {
        text: 'Core Concepts',
        collapsed: false,
        items: [
          { text: 'DTO Mapping', link: '/documentation/dto-mapping' },
          { text: 'Source Generators', link: '/documentation/source-generators' },
          { text: 'Table-Valued Parameters', link: '/documentation/tvp' }
        ]
      },
      {
        text: 'Guides',
        collapsed: false,
        items: [
          { text: 'Reading Data', link: '/documentation/reading-data' },
          { text: 'Writing Data', link: '/documentation/writing-data' },
          { text: 'Multiple Result Sets', link: '/documentation/multi-results' },
          { text: 'Caching', link: '/documentation/cache' },
          { text: 'Aspire Integration', link: '/documentation/aspire' },
          { text: 'Advanced Usage', link: '/documentation/advanced-usage' }
        ]
      },
      {
        text: 'Reference',
        collapsed: true,
        items: [
          { text: 'API Reference', link: '/documentation/api' },
          { text: 'Best Practices', link: '/documentation/best-practices' }
        ]
      },
      {
        text: 'Performance',
        collapsed: false,
        items: [
          { text: 'Overview & Methodology', link: '/benchmarks/' },
          { text: 'In-Memory Benchmarks', link: '/benchmarks/in-memory' },
          { text: 'Collection Benchmarks', link: '/benchmarks/collections' },
          { text: 'SQL Server Benchmarks', link: '/benchmarks/sql-server' },
          { text: 'Cache Benchmarks', link: '/benchmarks/cache' }
        ]
      }
    ],

    search: {
      provider: 'local'
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/CaeriusNET/CaeriusNet' }
    ],

    lastUpdated: {
      text: 'Updated at',
      formatOptions: {
        dateStyle: 'full',
        timeStyle: 'medium'
      }
    },

    editLink: {
      pattern: 'https://github.com/CaeriusNET/CaeriusNet/edit/main/Documentations/docs/:path',
      text: 'Edit this page on GitHub'
    },

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2026 Johan (AriusII) Coureuil'
    }
  }
})

