<script lang="ts">
  import { checkSeal, type SealResult } from '../lib/seal'
  import { session } from '../lib/session.svelte'

  let code = $state('')
  let publicKey = $state('')
  let result = $state.raw<SealResult | null>(null)
  let busy = $state(false)

  // The site's own key is the default, but any key can be pasted in: the check never has to trust this server.
  $effect(() => {
    if (!publicKey && session.config) publicKey = session.config.publicKey
  })

  async function inspect(event: SubmitEvent) {
    event.preventDefault()
    busy = true
    try {
      result = await checkSeal(code, publicKey.trim())
    } finally {
      busy = false
    }
  }
</script>

<section class="card stack">
  <h1>Inspect a seal</h1>
  <p>
    Paste a treasure code or the one printed on a certificate. It is checked right here in yer browser against the
    site's public key; nothing is sent anywhere.
  </p>

  <form class="stack" onsubmit={inspect}>
    <div>
      <label for="code">Treasure code</label>
      <input id="code" type="text" bind:value={code} oninput={() => (result = null)} placeholder="P1R4T3-C0D3&#123;…&#125;" autocomplete="off" spellcheck="false" />
    </div>
    <div><button class="button" disabled={busy || code.trim() === '' || !publicKey}>Inspect the seal</button></div>
  </form>

  {#if result}
    <div class="verdict {result.status}" role="status">
      {#if result.status === 'genuine'}
        <h2>Aye, the mark be true!</h2>
        <p>
          This code was signed for <strong>@{result.seal.handle}</strong> (Telegram id {result.seal.telegramId}).
        </p>
      {:else if result.status === 'forged'}
        <h2>A forgery!</h2>
        <p>The signature does not match this site's key. This writ be fit for the fishes.</p>
      {:else if result.status === 'unsupported'}
        <h2>Yer browser can't check it</h2>
        <p>It lacks Ed25519 support. Update it, or verify with OpenSSL as described in the README.</p>
      {:else}
        <h2>That be not a treasure code</h2>
        <p>Make sure ye copied the whole thing, from <code>P1R4T3-C0D3&#123;</code> to the closing brace.</p>
      {/if}
    </div>
  {/if}

  <details>
    <summary>The public key</summary>
    <p class="small muted">
      Codes are Ed25519 signatures, so anyone with this key can verify them, with or without this site. Keep a copy if
      ye want to be able to check certificates in the future.
    </p>
    <input type="text" bind:value={publicKey} aria-label="Ed25519 public key, base64" spellcheck="false" autocomplete="off" />
  </details>
</section>

<style>
  .verdict {
    padding: 1em 1.2em;
    border-radius: 10px;
    border-left: 6px solid;
  }

  .verdict h2 { margin-bottom: 0.2em; }
  .verdict p { margin: 0; }

  .genuine { background: rgba(46, 125, 79, 0.16); border-color: var(--ok); }
  .forged,
  .malformed,
  .unsupported { background: rgba(168, 40, 43, 0.12); border-color: var(--blood); }

  summary { cursor: pointer; font-weight: 700; }
  details input { margin-top: 0.5em; font-family: var(--font-mono); font-size: 0.85em; }
</style>
