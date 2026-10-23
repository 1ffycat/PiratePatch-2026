<script lang="ts">
  import { api, ApiError, type Leaderboard } from '../lib/api'
  import Notice from '../lib/Notice.svelte'

  const PAGE = 100

  let board = $state.raw<Leaderboard | null>(null)
  let error = $state<string | null>(null)
  let loading = $state(true)

  async function load(more = false) {
    loading = true
    try {
      const next = await api.leaderboard(PAGE, more && board ? board.entries.length : 0)
      board = more && board ? { total: next.total, entries: [...board.entries, ...next.entries] } : next
    } catch (e) {
      error = e instanceof ApiError ? e.message : 'Could not read the ledger.'
    } finally {
      loading = false
    }
  }

  $effect(() => {
    void load()
  })

  const fmt = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })
</script>

<section class="card stack">
  <h1>Hall of Fame</h1>
  <p class="muted">Every soul that cracked the program, in the order they did it.</p>

  {#if error}
    <Notice>{error}</Notice>
  {:else if board && board.entries.length === 0}
    <p>Nobody yet. Will ye be the first?</p>
  {:else if board}
    <div class="table-wrap">
      <table>
        <thead>
          <tr><th>#</th><th>Pirate</th><th>Cracked it</th><th><span class="sr-only">Certificate</span></th></tr>
        </thead>
        <tbody>
          {#each board.entries as entry (entry.rank)}
            <tr>
              <td class="rank">{entry.rank}</td>
              <td><a href="https://t.me/{entry.handle}" target="_blank" rel="noopener noreferrer">@{entry.handle}</a></td>
              <td class="muted">{fmt.format(new Date(entry.solvedAt))}</td>
              <td><a href={entry.certificateUrl} target="_blank" rel="noopener">📜 Certificate</a></td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>

    {#if board.entries.length < board.total}
      <div><button class="button ghost" onclick={() => load(true)} disabled={loading}>Show more</button></div>
    {/if}
  {:else}
    <p class="muted">Readin' the ledger…</p>
  {/if}
</section>

<style>
  .table-wrap { overflow-x: auto; }

  table {
    width: 100%;
    border-collapse: collapse;
  }

  th,
  td {
    padding: 0.6em 0.8em;
    text-align: left;
    border-bottom: 1px dashed var(--parchment-edge);
    white-space: nowrap;
  }

  th {
    font-family: var(--font-display);
    font-size: 1.15rem;
    font-weight: 400;
  }

  .rank {
    font-family: var(--font-display);
    font-size: 1.3rem;
    color: var(--gold-deep);
  }

  .sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    overflow: hidden;
    clip-path: inset(50%);
  }
</style>
