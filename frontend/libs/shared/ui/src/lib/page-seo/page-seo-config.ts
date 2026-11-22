import { InjectionToken, type Provider } from '@angular/core'

export type PageSeoImage = {
	readonly url: string
	readonly alt?: string | null
	readonly width?: number | null
	readonly height?: number | null
}

export type PageSeoType = 'website' | 'article'

export type PageSeoJsonLd = Record<string, unknown> | readonly Record<string, unknown>[]

export type PageSeoConfig = {
	readonly siteName: string
	readonly defaultDescription: string
	readonly defaultImage?: string | PageSeoImage | null
	readonly locale?: string
	readonly origin?: string | null
}

export const PAGE_SEO_CONFIG = new InjectionToken<PageSeoConfig>('PAGE_SEO_CONFIG', {
	providedIn: 'root',
	factory: (): PageSeoConfig => ({
		siteName: 'GameAtlas',
		defaultDescription:
			'Explore detailed interactive video game maps with collectibles, quests, bosses, and hidden locations.',
		locale: 'en_US',
	}),
})

export function providePageSeo(config: Partial<PageSeoConfig>): Provider {
	return {
		provide: PAGE_SEO_CONFIG,
		useValue: {
			siteName: config.siteName ?? 'GameAtlas',
			defaultDescription:
				config.defaultDescription ??
				'Explore detailed interactive video game maps with collectibles, quests, bosses, and hidden locations.',
			defaultImage: config.defaultImage ?? null,
			locale: config.locale ?? 'en_US',
			origin: config.origin ?? null,
		},
	}
}
