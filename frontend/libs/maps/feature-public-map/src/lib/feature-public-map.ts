import { HttpStatusCode } from '@angular/common/http'
import { Component, computed, inject, input } from '@angular/core'
import { RouterLink } from '@angular/router'
import { PublicMapsQueryOptions } from '@pinveria/maps/data-access'
import { Button, PageSeo, PageSeoDirective } from '@pinveria/shared/ui'
import { buildMapOgImagePath } from '@pinveria/shared/util'
import { HlmEmptyImports } from '@spartan-ng/helm/empty'
import { HlmSpinner } from '@spartan-ng/helm/spinner'
import { injectQuery } from '@tanstack/angular-query'
import type { PublicMapChoice } from './category-filter-panel/category-filter-panel'
import { PublicMapViewer } from './public-map-viewer/public-map-viewer'

export type PublicMapGame = {
	name: string
	coverUrl: string
	bannerUrl: string | null
	maps: readonly PublicMapChoice[]
}

@Component({
	selector: 'maps-feature-public-map',
	imports: [PublicMapViewer, Button, RouterLink, PageSeoDirective, HlmEmptyImports, HlmSpinner],
	templateUrl: './feature-public-map.html',
	providers: [PublicMapsQueryOptions],
	host: { class: 'relative block h-full w-full overflow-hidden' },
})
export class FeaturePublicMap {
	readonly slug = input.required<string>()
	readonly mapSlug = input.required<string>()
	readonly gameTitle = input<string>()
	readonly gameCover = input<string | null>(null)
	readonly game = input<PublicMapGame | null>(null)

	readonly #mapOptions = inject(PublicMapsQueryOptions)
	readonly #pageSeo = inject(PageSeo)

	protected readonly mapDetail = injectQuery(() => this.#mapOptions.getDetail(this.slug(), this.mapSlug()))

	protected readonly httpStatusCode = HttpStatusCode

	protected readonly resolvedGameName = computed(() => this.game()?.name ?? this.gameTitle() ?? this.slug())

	protected readonly seoTitle = computed(() => {
		const map = this.mapDetail.data()

		if (!map) return null

		const gameName = this.resolvedGameName()

		return gameName ? `${map.name} - ${gameName}` : map.name
	})

	protected readonly seoDescription = computed(() => {
		const map = this.mapDetail.data()

		if (!map) return null

		if (map.description?.trim()) return map.description.trim()

		const gameName = this.resolvedGameName()

		return `Interactive map of ${map.name} for ${gameName} on Pinveria. Explore locations, collectibles, quests, and markers.`
	})

	protected readonly seoImage = computed(() => {
		const map = this.mapDetail.data()

		if (!map) return null

		const game = this.game()
		const categoriesCount = map.categoryGroups.reduce((sum, group) => sum + group.categories.length, 0)
		const selectedStyle = map.styles.find(style => style.id === map.defaultStyleId) ?? map.styles[0] ?? null
		const summaryPreview = game?.maps.find(item => item.slug === map.slug)?.preview?.url ?? null
		const previewUrl = summaryPreview ?? selectedStyle?.preview.url ?? null

		return buildMapOgImagePath(this.slug(), map.slug, {
			gameName: this.resolvedGameName(),
			mapName: map.name,
			markersCount: map.markers.length,
			categoriesCount,
			previewUrl,
			gameBannerUrl: game?.bannerUrl ?? null,
			gameCoverUrl: game?.coverUrl ?? this.gameCover() ?? null,
		})
	})

	protected readonly seoJsonLd = computed(() => {
		const map = this.mapDetail.data()

		if (!map) return null

		const gameName = this.resolvedGameName()
		const homeUrl = this.#pageSeo.resolveAbsoluteUrl('/')
		const gameUrl = this.#pageSeo.resolveAbsoluteUrl(`/games/${this.slug()}`)
		const mapUrl = this.#pageSeo.resolveAbsoluteUrl(`/games/${this.slug()}/maps/${map.slug}`)
		const description = this.seoDescription()
		const ogImage = this.seoImage()

		const mapSchema: Record<string, unknown> = {
			'@context': 'https://schema.org',
			'@type': 'Map',
			name: `${map.name} - ${gameName}`,
			url: mapUrl,
		}

		if (description) {
			mapSchema['description'] = description
		}

		if (ogImage) {
			mapSchema['image'] = this.#pageSeo.resolveAbsoluteUrl(ogImage)
		}

		const breadcrumb = {
			'@context': 'https://schema.org',
			'@type': 'BreadcrumbList',
			itemListElement: [
				{
					'@type': 'ListItem',
					position: 1,
					name: 'Games',
					item: homeUrl,
				},
				{
					'@type': 'ListItem',
					position: 2,
					name: gameName,
					item: gameUrl,
				},
				{
					'@type': 'ListItem',
					position: 3,
					name: map.name,
					item: mapUrl,
				},
			],
		}

		return [mapSchema, breadcrumb]
	})
}
