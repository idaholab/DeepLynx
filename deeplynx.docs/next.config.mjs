import nextra from 'nextra'

const withNextra = nextra({
  search: { codeblocks: true }
})

export default withNextra({
  // Add regular Next.js options here
  basePath: '/docs',
  reactStrictMode: true,

  async redirects() {
    return [
      {
        source: '/',
        destination: '/docs',
        permanent: false, // 307, one day set true for 308 to hard cache browser/cdn redirects
        basePath: false,  // critical: match the real "/" (outside the basePath)
      },
    ]
  },
  output: 'standalone',
})
