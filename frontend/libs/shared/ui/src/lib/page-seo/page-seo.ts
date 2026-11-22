import {
	DOCUMENT,
	effect,
	inject,
	Injectable,
	REQUEST,
	RESPONSE_INIT,
	type Signal,
	signal,
	untracked,
} from '@angular/core'
import { Meta } from '@angular/platform-browser'
import { PageTitleStrategy } from '../page-title/page-title-strategy'
import { PAGE_SEO_CONFIG, type PageSeoImage, type PageSeoJsonLd, type PageSeoType } from './page-seo-config'

export type PageSeoState = {
	readonly title?: string | null
	readonly description?: string | null
	readonly image?: string | PageSeoImage | null
	readonly type?: PageSeoType
	readonly canonical?: string | null
	readonly noindex?: boolean
	readonly nofollow?: boolean
	readonly statusCode?: number | null
	readonly jsonLd?: PageSeoJsonLd | null
}

const MAX_DESCRIPTION_LENGTH = 200
const JSON_LD_SELECTOR = 'script[type="application/ld+json"][data-page-seo="json-ld"]'

function normalizeOrigin(origin: string | null | undefined): string {
	return (origin ?? '').trim().replace(/\/+$/, '')
}

function normalizeDescription(raw: string | null | undefined, fallback: string): string {
	const cleaned = (raw ?? '').replace(/\s+/g, ' ').trim()
	const text = cleaned || fallback

	if (text.length <= MAX_DESCRIPTION_LENGTH) return text

	return `${text.slice(0, MAX_DESCRIPTION_LENGTH - 1).trimEnd()}…`
}

function serializeJsonLd(payload: PageSeoJsonLd): string {
	return JSON.stringify(payload).replace(/</g, '\\u003c')
}

@Injectable({ providedIn: 'root' })
export class PageSeo {
	readonly #config = inject(PAGE_SEO_CONFIG)
	readonly #meta = inject(Meta)
	readonly #document = inject(DOCUMENT)
	readonly #request = inject(REQUEST, { optional: true })
	readonly #responseInit = inject(RESPONSE_INIT, { optional: true })
	readonly #titleStrategy = inject(PageTitleStrategy, { optional: true })

	readonly #sources = signal<readonly Signal<PageSeoState>[]>([])

	constructor() {
		effect(() => {
			const sources = this.#sources()
			const activeSource = sources[sources.length - 1]
			const state = activeSource ? activeSource() : null
			const currentTitle = this.#titleStrategy?.title() ?? this.#config.siteName

			untracked(() => this.#apply(state, currentTitle))
		})
	}

	register(source: Signal<PageSeoState>): () => void {
		this.#sources.update(sources => [...sources, source])

		return () => {
			this.#sources.update(sources => sources.filter(item => item !== source))
		}
	}

	resolveOrigin(): string {
		const configured = normalizeOrigin(this.#config.origin)

		if (configured) return configured

		if (this.#request) {
			const forwardedProto = this.#request.headers.get('x-forwarded-proto')?.split(',')[0]?.trim()
			const forwardedHost = this.#request.headers.get('x-forwarded-host')?.split(',')[0]?.trim()
			const host = forwardedHost || this.#request.headers.get('host')?.trim()

			if (host) {
				const protocol = forwardedProto || (this.#request.url.startsWith('https:') ? 'https' : 'http')

				return normalizeOrigin(`${protocol}://${host}`)
			}

			try {
				return normalizeOrigin(new URL(this.#request.url).origin)
			} catch {
				// Ignore invalid request URL
			}
		}

		const locationOrigin = normalizeOrigin(this.#document.defaultView?.location?.origin)

		return locationOrigin
	}

	resolveAbsoluteUrl(pathOrUrl: string): string {
		const trimmed = pathOrUrl.trim()

		if (/^https?:\/\//i.test(trimmed)) return trimmed

		const origin = this.resolveOrigin()
		const normalizedPath = trimmed.startsWith('/') ? trimmed : `/${trimmed}`

		return origin ? `${origin}${normalizedPath}` : normalizedPath
	}

	#resolveCurrentPathname(): string {
		if (this.#request) {
			try {
				return new URL(this.#request.url).pathname || '/'
			} catch {
				// Fall through
			}
		}

		return this.#document.defaultView?.location?.pathname || '/'
	}

	#apply(state: PageSeoState | null, fallbackFormattedTitle: string): void {
		if (state?.title !== undefined && state.title !== null) {
			this.#titleStrategy?.setPageTitle(state.title)
		}

		const formattedTitle =
			state?.title !== undefined && state.title !== null
				? (this.#titleStrategy?.title() ?? `${state.title} | ${this.#config.siteName}`)
				: fallbackFormattedTitle || this.#config.siteName

		const description = normalizeDescription(state?.description, this.#config.defaultDescription)
		const canonicalUrl = this.resolveAbsoluteUrl(state?.canonical || this.#resolveCurrentPathname())
		const robots = `${state?.noindex ? 'noindex' : 'index'}, ${state?.nofollow ? 'nofollow' : 'follow'}`
		const type: PageSeoType = state?.type ?? 'website'
		const rawImage = state?.image ?? this.#config.defaultImage ?? null

		const resolvedImage: PageSeoImage | null =
			typeof rawImage === 'string'
				? { url: this.resolveAbsoluteUrl(rawImage), width: 1200, height: 630 }
				: rawImage
					? {
							url: this.resolveAbsoluteUrl(rawImage.url),
							alt: rawImage.alt ?? null,
							width: rawImage.width ?? null,
							height: rawImage.height ?? null,
						}
					: null

		this.#setNameMeta('description', description)
		this.#setNameMeta('robots', robots)

		this.#setPropertyMeta('og:site_name', this.#config.siteName)
		this.#setPropertyMeta('og:type', type)
		this.#setPropertyMeta('og:title', formattedTitle)
		this.#setPropertyMeta('og:description', description)
		this.#setPropertyMeta('og:url', canonicalUrl)

		if (this.#config.locale) {
			this.#setPropertyMeta('og:locale', this.#config.locale)
		}

		if (resolvedImage) {
			this.#setPropertyMeta('og:image', resolvedImage.url)
			this.#setPropertyMeta('og:image:alt', resolvedImage.alt || formattedTitle)

			if (resolvedImage.width) {
				this.#setPropertyMeta('og:image:width', String(resolvedImage.width))
			} else {
				this.#removePropertyMeta('og:image:width')
			}

			if (resolvedImage.height) {
				this.#setPropertyMeta('og:image:height', String(resolvedImage.height))
			} else {
				this.#removePropertyMeta('og:image:height')
			}
		} else {
			this.#removePropertyMeta('og:image')
			this.#removePropertyMeta('og:image:alt')
			this.#removePropertyMeta('og:image:width')
			this.#removePropertyMeta('og:image:height')
		}

		this.#setNameMeta('twitter:card', resolvedImage ? 'summary_large_image' : 'summary')
		this.#setNameMeta('twitter:title', formattedTitle)
		this.#setNameMeta('twitter:description', description)

		if (resolvedImage) {
			this.#setNameMeta('twitter:image', resolvedImage.url)
			this.#setNameMeta('twitter:image:alt', resolvedImage.alt || formattedTitle)
		} else {
			this.#removeNameMeta('twitter:image')
			this.#removeNameMeta('twitter:image:alt')
		}

		this.#updateCanonicalLink(canonicalUrl)
		this.#updateJsonLd(state?.jsonLd ?? null)

		if (this.#responseInit && state?.statusCode) {
			this.#responseInit.status = state.statusCode
		}
	}

	#setNameMeta(name: string, content: string): void {
		this.#meta.updateTag({ name, content }, `name='${name}'`)
	}

	#removeNameMeta(name: string): void {
		this.#meta.removeTag(`name='${name}'`)
	}

	#setPropertyMeta(property: string, content: string): void {
		this.#meta.updateTag({ property, content }, `property='${property}'`)
	}

	#removePropertyMeta(property: string): void {
		this.#meta.removeTag(`property='${property}'`)
	}

	#updateCanonicalLink(href: string): void {
		const head = this.#document.head

		if (!head) return

		let link = head.querySelector<HTMLLinkElement>('link[rel="canonical"]')

		if (!link) {
			link = this.#document.createElement('link')
			link.setAttribute('rel', 'canonical')
			head.appendChild(link)
		}

		link.setAttribute('href', href)
	}

	#updateJsonLd(jsonLd: PageSeoJsonLd | null): void {
		const head = this.#document.head

		if (!head) return

		const existing = head.querySelector<HTMLScriptElement>(JSON_LD_SELECTOR)

		if (!jsonLd || (Array.isArray(jsonLd) && jsonLd.length === 0)) {
			existing?.remove()

			return
		}

		const script = existing ?? this.#document.createElement('script')

		script.setAttribute('type', 'application/ld+json')
		script.setAttribute('data-page-seo', 'json-ld')
		script.textContent = serializeJsonLd(jsonLd)

		if (!existing) {
			head.appendChild(script)
		}
	}
}
