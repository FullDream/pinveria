import { booleanAttribute, computed, DestroyRef, Directive, inject, input, numberAttribute } from '@angular/core'
import { PageSeo, type PageSeoState } from './page-seo'
import type { PageSeoImage, PageSeoJsonLd, PageSeoType } from './page-seo-config'

@Directive({ selector: 'ng-container[uiPageSeo]' })
export class PageSeoDirective {
	readonly title = input<string | null>()
	readonly description = input<string | null>()
	readonly image = input<string | PageSeoImage | null>()
	readonly type = input<PageSeoType>('website')
	readonly canonical = input<string | null>()
	readonly noindex = input(false, { transform: booleanAttribute })
	readonly nofollow = input(false, { transform: booleanAttribute })
	readonly statusCode = input<number, unknown>(undefined, { transform: numberAttribute })
	readonly jsonLd = input<PageSeoJsonLd | null>()

	readonly #state = computed<PageSeoState>(() => ({
		title: this.title(),
		description: this.description(),
		image: this.image(),
		type: this.type(),
		canonical: this.canonical(),
		noindex: this.noindex(),
		nofollow: this.nofollow(),
		statusCode: Number.isFinite(this.statusCode()) ? this.statusCode() : null,
		jsonLd: this.jsonLd(),
	}))

	constructor() {
		const unregister = inject(PageSeo).register(this.#state)

		inject(DestroyRef).onDestroy(unregister)
	}
}
