export default {
  getting_started: { title: "Getting Started with DeepLynx Nexus" },
  managing_deeplynx_nexus: { title: "Managing DeepLynx Nexus" },
  developing_deeplynx_nexus: { title: "Developing with DeepLynx Nexus" },
  about_deeplynx_nexus: { title: "About DeepLynx Nexus" },
  deeplynx: {
    title: "Back to DeepLynx",
    type: "page",
    href: process.env.NEXT_PUBLIC_URL
      ? process.env.NEXT_PUBLIC_URL
      : "http://localhost:3000",
  },
};
