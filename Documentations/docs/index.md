---
layout: home

hero:
  name: "Caerius.NET"
  text: "C# & TSQL enchanced by performance Micro-ORM"
  tagline: Stored Procedure to DTO in µseconds !
  image:
    alt: Caerius Logo
  actions:
    - theme: brand
      text: What is Caerius.NET?
      link: /quickstart/what-is-caeriusnet
    - theme: alt
      text: Quickstart
      link: /quickstart/getting-started
    - theme: alt
      text: Github
      link: https://github.com/CaeriusNET/CaeriusNet

features:
  - icon: 🛠️
    title: Two codes, One goal
    details: Make </br> your Stored Procedure in TSQL, </br> your Data Transfer Object in C#, </br> with your favorites tools.
  - icon: 🚀
    title: C# mapping in µseconds
    details: Let us do the heavy lifting for you, and focus on your business logic.
  - icon: 💪
    title: Heavy sets of data
    details: Caerius.NET is optimized for heavy sets of data, and will not slow down your application.</br>You don't trust us ?</br>We have plenty of benchmarks.
  - icon: 🔄
    title: Only Asynchronous
    details: Cause we cannot know how long your Stored Procedure will take. We only provide asynchronous methods.
---

<style>
:root {
  --vp-home-hero-name-color: transparent;
  --vp-home-hero-name-background: -webkit-linear-gradient(120deg, #8f00fd 30%, #6ae8f4);

  --vp-home-hero-image-background-image: linear-gradient(-45deg, #8f00fd 50%, #6ae8f4 50%);
  --vp-home-hero-image-filter: blur(44px);
}

@media (min-width: 640px) {
  :root {
    --vp-home-hero-image-filter: blur(56px);
  }
}

@media (min-width: 960px) {
  :root {
    --vp-home-hero-image-filter: blur(68px);
  }
}
</style>