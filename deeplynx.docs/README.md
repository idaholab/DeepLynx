# DeepLynx Documentation

This is a [Nextra](https://nextra.site/) documentation site. Nextra is a static site generator, that compiles JavaScript enabled markdown applications into a recognizable documentation website. To contribute to Nexus docs, consider using the Nextra built-in components for styles and organizational components.

To run this application, in the `deeplynx.docs` directory, run `npm install`, then `npm run dev`. Navigate to `http://localhost:3001`.

Because the focus of this application is plaintext documentation for Nexus, before committing changes, please use an AI tool like Claude Code or Codex to review for language constructs like grammar and typos.

### How Nextra Works

The Nextra application takes content from the `/content` directory. In each content subdirectory, a `_meta.ts` tells Nextra what lives there. The meta object should point to lower subdirectories, or `.mdx` files. The MDX files are JavaScript enabled markdown files that you can use to write documentation.

### What is deeplynx.docs for?

This website is built into the deployment and served under the `/docs` route, like `https://deeplynx.inl.gov/docs`. The documentation site is for information about Nexus, or high-level guidance for users or developers.

Technical documentation about the Nexus API should be documented in Scalar, or under the root `CONTRIBUTING.md`.
