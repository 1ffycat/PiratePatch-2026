<script lang="ts">
  import type { Snippet } from 'svelte'
  import { slide } from 'svelte/transition'

  let { kind = 'error', children }: { kind?: 'error' | 'ok'; children: Snippet } = $props()
</script>

<p class="notice {kind}" role={kind === 'error' ? 'alert' : 'status'} transition:slide={{ duration: 180 }}>
  {@render children()}
</p>

<style>
  .notice {
    margin: 0;
    padding: 0.7em 1em;
    font-weight: 700;
    border-radius: 10px;
    border-left: 6px solid;
  }

  .error {
    color: #6d1417;
    background: rgba(168, 40, 43, 0.14);
    border-color: var(--blood);
  }

  .ok {
    color: #1c5233;
    background: rgba(46, 125, 79, 0.16);
    border-color: var(--ok);
  }
</style>
