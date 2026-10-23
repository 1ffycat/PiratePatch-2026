<script lang="ts">
  import { slide } from 'svelte/transition'

  let open = $state(false)
</script>

<section class="hints">
  {#if !open}
    <p class="muted">Stuck, or never opened a disassembler in yer life? The quartermaster keeps a few pointers, but they spoil the fun.</p>
    <button class="button ghost" onclick={() => (open = true)}>Show me the hints</button>
  {:else}
    <div class="stack" transition:slide>
      <h3>The Quartermaster's Pointers</h3>

      <p>
        Needs Linux and an x86 CPU (most CPUs are x86). Ye can use WSL or a virtual machine.
      </p>
      <p>
        Try runnin' the program first, then <strong>patch out the password check</strong> and
        <strong>defuse the timebomb</strong>.
      </p>

      <h3>Radare2 tips</h3>
      <pre><code>r2 -w challenge        # open the program, writable
aaa                    # analyse it
afl                    # list all functions
s function_name        # seek to a function
pdf                    # print the function's disassembly
VV                     # graph view (q twice to leave)
wa mov eax, 0 @ 0x1337 # patch: assemble at the given address
q                      # quit</code></pre>
      <p class="small muted">
        Radare2 writes numbers with a <code>0x</code> prefix for hexadecimal and none for decimal, so
        <code>0x42</code> and <code>66</code> are the same.
      </p>

      <h3>Patches that often do the trick</h3>
      <ul>
        <li>Swap <code>je</code> (jump if equal) for <code>jne</code>, and the other way round.</li>
        <li>Turn <code>je</code>/<code>jne</code> into an unconditional <code>jmp</code>.</li>
        <li>Replace a jump with <code>nop</code>s, so the program just carries on.</li>
        <li>Change <code>mov eax, 0</code> to <code>mov eax, 1</code> or back; <code>eax</code> usually holds a function's result.</li>
        <li>Change a number: <code>cmp ebx, 0x42</code> becomes <code>cmp ebx, 2027</code>.</li>
      </ul>

      <p class="warning">
        <strong>Mind the size of the instruction!</strong> Check its bytes in hex. Instructions take 2, 4, 6 or 8
        bytes, and writin' a shorter one over a longer one corrupts the program. The jump addresses be static, so a
        size mismatch throws them off. Fill the gap with <code>nop</code>s:
        <code>wa mov eax, ebx; nop @ 0x1337</code>.
      </p>
    </div>
  {/if}
</section>

<style>
  .warning {
    padding: 0.8em 1em;
    background: rgba(168, 40, 43, 0.1);
    border-left: 6px solid var(--blood);
    border-radius: 10px;
  }

  ul {
    padding-left: 1.2em;
    margin: 0;
  }
</style>
