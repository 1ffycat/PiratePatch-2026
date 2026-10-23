<script lang="ts">
  import type { Attachment } from 'svelte/attachments'

  let { botUsername, onauth }: { botUsername: string; onauth: (user: Record<string, unknown>) => void } = $props()

  // Telegram's widget finds its callback by global name, so it has to live on window for as long as the widget does.
  const widget: Attachment<HTMLElement> = (container) => {
    const callback = '__onTelegramAuth'
    ;(window as unknown as Record<string, unknown>)[callback] = onauth

    const script = document.createElement('script')
    script.async = true
    script.src = 'https://telegram.org/js/telegram-widget.js?22'
    script.dataset.telegramLogin = botUsername
    script.dataset.size = 'large'
    script.dataset.userpic = 'false'
    script.dataset.radius = '10'
    script.dataset.onauth = `${callback}(user)`
    container.append(script)

    return () => {
      delete (window as unknown as Record<string, unknown>)[callback]
      container.replaceChildren()
    }
  }
</script>

<div class="widget" {@attach widget}></div>

<style>
  .widget {
    min-height: 40px;
  }
</style>
