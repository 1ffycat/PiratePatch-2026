<script lang="ts">
  import { link } from '../lib/router.svelte'
</script>

<div class="stack">
  <section class="card hero">
    <p class="eyebrow">A reverse-engineering CTF</p>
    <h1>Crack the program.<br />Claim the treasure.</h1>
    <p class="lead">
      Ye'll get one small program, built for ye alone. Somewhere inside it be a treasure code, locked behind a
      secret ye don't have. No source code, no map. Just the program, yer wits, and whatever tools ye can lay hands on.
    </p>
    <div class="row">
      <a class="button" href="/challenge" {@attach link}>Set sail</a>
      <a class="button ghost" href="/leaderboard" {@attach link}>Hall of Fame</a>
    </div>
  </section>

  <section class="card stack">
    <h2>What be this?</h2>
    <p>
      A <strong>capture-the-flag</strong> puzzle, the kind security folk use to practise: ye hunt for a hidden
      string, the <em>flag</em>, and prove ye found it. This one is about <strong>binary patching</strong>, bendin' a
      compiled program to yer will without ever seein' its source. It started life as the hands-on part of a webinar
      on reverse engineering, and it's now kept here as an archive for anyone who wants to give it a go.
    </p>
    <p>
      <strong>The goal:</strong> make yer program reveal its treasure code. It looks like
      <code>P1R4T3-C0D3&#123;…&#125;</code>. Bring it back here and ye'll be awarded a signed certificate and a place in the
      Hall of Fame.
    </p>
  </section>

  <section class="card stack">
    <h2>How it goes</h2>
    <ol class="steps">
      <li><strong>Sign in with Telegram.</strong> That's how the certificate knows yer name.</li>
      <li><strong>Download yer program.</strong> It's yours alone: its code only works for ye.</li>
      <li><strong>Work out how to make it talk.</strong> Run it, poke at it, take it apart.</li>
      <li><strong>Submit the code</strong> it gives up, and collect yer certificate.</li>
    </ol>
    <p class="muted small">There be no clues in this brief. The challenge page has optional hints, kept behind a button.</p>
  </section>

  <section class="card stack">
    <h2>What ye need</h2>
    <ul>
      <li>
        A <strong>Linux machine on an x86-64 CPU</strong>, from 2022 or newer (glibc 2.34+: Ubuntu 22.04, Debian 12,
        Fedora 35, RHEL 9 and later all qualify; Alpine doesn't). A virtual machine or WSL on Windows does the job.
      </li>
      <li>A terminal, and a tool for lookin' inside programs. Disassemblers and debuggers such as Ghidra, radare2, gdb or objdump all work.</li>
      <li>Patience. A little assembly helps, but ye can learn it along the way.</li>
    </ul>
    <p>
      <strong>A word of caution:</strong> the program only reads from the keyboard and writes to the screen, but running
      programs ye downloaded from the internet in a throwaway VM is a good habit anyway.
    </p>
  </section>

  <section class="card stack">
    <h2>The fine print</h2>
    <ul>
      <li>
        Signin' in shares yer public <strong>@username</strong> and Telegram id, nothing more. The username goes on the
        certificate and, once ye've solved it, onto the public Hall of Fame. There's one session cookie and no tracking.
      </li>
      <li>Every code is signed for one player. Sharin' it with others gets them nowhere.</li>
      <li>
        A certificate can be checked by anyone, and without this site, thanks to a public key.
        <a href="/verify" {@attach link}>Inspect a seal</a>.
      </li>
    </ul>
  </section>
</div>

<style>
  .eyebrow {
    margin: 0 0 0.4em;
    font-variant: small-caps;
    letter-spacing: 0.12em;
    color: var(--blood);
  }

  .hero h1 { margin-bottom: 0.4em; }
  .lead { font-size: 1.3rem; }

  .steps {
    margin: 0;
    padding-left: 1.4em;
  }

  .steps li { margin-bottom: 0.5em; }
  ul { margin: 0; padding-left: 1.2em; }
</style>
