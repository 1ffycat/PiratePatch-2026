<script lang="ts">
  import { fade } from 'svelte/transition'
  import { api, ApiError, BINARY_URL, type Solve } from '../lib/api'
  import { session } from '../lib/session.svelte'
  import { link } from '../lib/router.svelte'
  import Hints from '../lib/Hints.svelte'
  import Notice from '../lib/Notice.svelte'
  import TelegramLogin from '../lib/TelegramLogin.svelte'

  let code = $state('')
  let busy = $state(false)
  let error = $state<string | null>(null)
  let devHandle = $state('')

  const player = $derived(session.me.player)
  const solve = $derived(session.me.solve)

  async function attempt<T>(action: () => Promise<T>): Promise<T | undefined> {
    busy = true
    error = null
    try {
      return await action()
    } catch (e) {
      error = e instanceof ApiError ? e.message : 'Avast ye! Something went adrift.'
    } finally {
      busy = false
    }
  }

  async function login(request: () => Promise<typeof session.me>) {
    const me = await attempt(request)
    if (me) session.me = me
  }

  async function submit(event: SubmitEvent) {
    event.preventDefault()
    const result = await attempt(() => api.submit(code.trim()))
    if (!result) return
    if (result.success && result.solve) {
      session.me = { ...session.me, solve: result.solve }
    } else {
      error = 'Shiver me timbers! That code be as fake as a kraken\'s tear. Try again!'
    }
  }

  const solvedOn = (s: Solve) => new Date(s.solvedAt).toLocaleDateString(undefined, { dateStyle: 'long' })
</script>

<div class="stack">
  {#if !session.ready}
    <section class="card"><p class="muted">Hoistin' the sails…</p></section>
  {:else if session.failed || !session.config}
    <section class="card"><Notice>The harbour be closed right now. Try again later.</Notice></section>
  {:else if !player}
    <section class="card stack" in:fade>
      <h2>Show us yer colours</h2>
      <p>
        Sign in with Telegram to get yer personal program. Only yer public <strong>@username</strong> is shared; it goes
        on yer certificate and, once ye've solved the challenge, the public Hall of Fame.
      </p>
      <p class="small muted">An account needs a public username (Telegram: Settings → Username) so the certificate can name ye.</p>

      <TelegramLogin botUsername={session.config.botUsername} onauth={(user) => login(() => api.loginWithTelegram(user))} />

      {#if session.config.devLogin}
        <form class="stack dev" onsubmit={(e) => { e.preventDefault(); login(() => api.loginForDevelopment(devHandle)) }}>
          <label for="dev">Development login (this server runs in development mode)</label>
          <div class="row">
            <input id="dev" type="text" bind:value={devHandle} placeholder="pirate_handle" autocomplete="off" />
            <button class="button ghost" disabled={busy}>Log in</button>
          </div>
        </form>
      {/if}

      {#if error}<Notice>{error}</Notice>{/if}
    </section>
  {:else}
    <section class="card stack" in:fade>
      <div class="row between">
        <h2>Ahoy, @{player.handle}!</h2>
        <button class="button ghost small-button" onclick={() => session.logout()}>Sign out</button>
      </div>

      {#if solve}
        <Notice kind="ok">Ye cracked it on {solvedOn(solve)}, and ye be number {solve.rank} to do so!</Notice>
        <p>Yer certificate be signed and sealed. Show it off, or let anyone check it.</p>
        <div class="row">
          <a class="button" href={solve.certificateUrl} target="_blank" rel="noopener">View yer certificate (PDF)</a>
          <a class="button ghost" href="/leaderboard" {@attach link}>Hall of Fame</a>
        </div>
      {:else}
        <p>Yer program be ready. It's made for ye alone, and so is the treasure code inside it.</p>
      {/if}
    </section>

    {#if !solve}
      <section class="card stack" in:fade>
        <h2>1. Download yer program</h2>
        <a class="button" href={BINARY_URL} download="challenge">Download <code>challenge</code></a>
        <p>Then, on a Linux x86-64 machine:</p>
        <pre><code>chmod +x challenge
./challenge</code></pre>
        <p class="muted small">Downloadin' again gives ye the very same program.</p>
        <Hints />
      </section>

      <section class="card stack" in:fade>
        <h2>2. Present the treasure code</h2>
        <form class="stack" onsubmit={submit}>
          <div>
            <label for="code">Treasure code</label>
            <input
              id="code"
              type="text"
              bind:value={code}
              oninput={() => (error = null)}
              placeholder="P1R4T3-C0D3&#123;…&#125;"
              autocomplete="off"
              spellcheck="false"
            />
          </div>
          {#if error}<Notice>{error}</Notice>{/if}
          <div><button class="button" disabled={busy || code.trim() === ''}>{busy ? 'Countin' + '’' + ' doubloons…' : 'Claim yer booty'}</button></div>
        </form>
      </section>
    {/if}
  {/if}
</div>

<style>
  .between { justify-content: space-between; }
  .between h2 { margin: 0; }
  .small-button { min-height: 40px; font-size: 1.1rem; }
  .dev { padding-top: 1rem; border-top: 2px dashed var(--parchment-edge); }
</style>
