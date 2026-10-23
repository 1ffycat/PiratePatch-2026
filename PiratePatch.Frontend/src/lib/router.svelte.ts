import type { Attachment } from 'svelte/attachments'

/** Minimal history-API router: the site has four pages and no nesting. */
class Router {
  path = $state(location.pathname)

  constructor() {
    addEventListener('popstate', () => (this.path = location.pathname))
  }

  navigate(to: string) {
    if (to === this.path) return
    history.pushState(null, '', to)
    this.path = to
    scrollTo({ top: 0 })
  }
}

export const router = new Router()

/** Turns a plain `<a href="/x">` into a client-side navigation: `<a href="/x" {@attach link}>`. */
export const link: Attachment<HTMLAnchorElement> = (anchor) => {
  const onclick = (event: MouseEvent) => {
    const modified = event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || event.button !== 0
    const url = new URL(anchor.href, location.href)
    if (modified || url.origin !== location.origin || anchor.target || anchor.hasAttribute('download')) return

    event.preventDefault()
    router.navigate(url.pathname)
  }

  anchor.addEventListener('click', onclick)
  return () => anchor.removeEventListener('click', onclick)
}
