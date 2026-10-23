<script lang="ts">
  import type { Component } from 'svelte'
  import { fade } from 'svelte/transition'
  import { link, router } from './lib/router.svelte'
  import { session } from './lib/session.svelte'
  import Home from './pages/Home.svelte'
  import Challenge from './pages/Challenge.svelte'
  import Leaderboard from './pages/Leaderboard.svelte'
  import Verify from './pages/Verify.svelte'

  const pages: Record<string, { title: string; component: Component }> = {
    '/': { title: 'The Brief', component: Home },
    '/challenge': { title: 'The Challenge', component: Challenge },
    '/leaderboard': { title: 'Hall of Fame', component: Leaderboard },
    '/verify': { title: 'Inspect a Seal', component: Verify },
  }

  const page = $derived(pages[router.path.replace(/(.)\/$/, '$1')])
  const Page = $derived(page?.component)

  $effect(() => {
    document.title = page ? `${page.title} · Cap'n Radare's Patchin' Challenge` : "Lost at sea · Cap'n Radare's Patchin' Challenge"
  })

  $effect(() => {
    void session.load()
  })
</script>

<div class="sea" aria-hidden="true"></div>

<header>
  <a class="brand" href="/" {@attach link}>
    <span class="flag" aria-hidden="true">🏴‍☠️</span>
    <span>Cap'n Radare's <em>Patchin' Challenge</em></span>
  </a>
  <nav aria-label="Main">
    {#each Object.entries(pages) as [path, { title }] (path)}
      <a href={path} {@attach link} aria-current={router.path === path ? 'page' : undefined}>{title}</a>
    {/each}
  </nav>
</header>

<main>
  <svelte:boundary>
    {#key router.path}
      <div in:fade={{ duration: 200 }}>
        {#if Page}
          <Page />
        {:else}
          <section class="card stack">
            <h1>Lost at sea</h1>
            <p>No such waters on this chart.</p>
            <a class="button" href="/" {@attach link}>Back to port</a>
          </section>
        {/if}
      </div>
    {/key}

    {#snippet failed(_error, reset)}
      <section class="card stack">
        <h1>Man overboard!</h1>
        <p>Something broke on our end. Give it another go.</p>
        <button class="button" onclick={reset}>Try again</button>
      </section>
    {/snippet}
  </svelte:boundary>
</main>

<footer>
  <p>
    Made by Cap'n Vsevolod's crew. Treasure codes are signed with Ed25519; check one on the
    <a href="/verify" {@attach link}>seal inspector</a>.
  </p>
</footer>

<style>
  .sea {
    position: fixed;
    inset: auto 0 0 0;
    height: 140px;
    z-index: -1;
    opacity: 0.55;
    pointer-events: none;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='1200' height='140' viewBox='0 0 1200 140'%3E%3Cpath d='M0 70 Q150 20 300 70 T600 70 T900 70 T1200 70 V140 H0Z' fill='%231f6f8b' fill-opacity='.35'/%3E%3Cpath d='M0 95 Q150 55 300 95 T600 95 T900 95 T1200 95 V140 H0Z' fill='%23123a52' fill-opacity='.7'/%3E%3C/svg%3E");
    background-repeat: repeat-x;
    background-size: 1200px 140px;
    animation: drift 40s linear infinite;
  }

  @keyframes drift {
    to { background-position-x: -1200px; }
  }

  header,
  main,
  footer {
    width: min(100% - 2 * var(--gutter), 760px);
    margin-inline: auto;
  }

  header {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: 8px 24px;
    padding-block: 20px;
  }

  .brand {
    display: flex;
    align-items: center;
    gap: 0.5em;
    font-family: var(--font-display);
    font-size: 1.5rem;
    line-height: 1.1;
    color: var(--parchment);
    text-decoration: none;
  }

  .brand em { color: var(--gold); font-style: normal; }
  .flag { font-size: 1.8rem; }

  nav {
    display: flex;
    flex-wrap: wrap;
    gap: 4px 6px;
  }

  nav a {
    padding: 0.2em 0.7em;
    color: var(--foam);
    text-decoration: none;
    border-radius: 8px;
  }

  nav a:hover { background: rgba(255, 255, 255, 0.08); color: #fff; }
  nav a[aria-current='page'] { color: var(--sea-900); background: var(--gold); }

  main { padding-block: 8px 48px; }

  footer {
    padding-block: 24px 48px;
    font-size: 0.95rem;
    color: var(--foam-dim);
    text-align: center;
  }
</style>
