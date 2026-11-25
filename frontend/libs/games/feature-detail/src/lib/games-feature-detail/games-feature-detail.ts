import { HttpStatusCode } from '@angular/common/http'
import { Component, computed, inject, input } from '@angular/core'
import { RouterLink } from '@angular/router'
import { PublicGamesQueryOptions } from '@pinveria/games/data-access'
import {
	Button,
	ErrorState,
	ImagePreview,
	MediaThumbnail,
	PageLoadingSource,
	PageSeo,
	PageSeoDirective,
} from '@pinveria/shared/ui'
import { buildGameOgImagePath, formatCount } from '@pinveria/shared/util'
import { NgIcon, provideIcons } from '@ng-icons/core'
import {
	matArrowBackRound,
	matArrowForwardRound,
	matCategoryRound,
	matCloudOffRound,
	matHomeRound,
	matLocationOnRound,
	matMapRound,
	matRefreshRound,
} from '@ng-icons/material-symbols/round'
import { HlmBreadcrumbImports } from '@spartan-ng/helm/breadcrumb'
import { HlmCardImports } from '@spartan-ng/helm/card'
import { HlmEmptyImports } from '@spartan-ng/helm/empty'
import { HlmSkeleton } from '@spartan-ng/helm/skeleton'
import { injectQuery } from '@tanstack/angular-query'

@Component({
	selector: 'games-detail',
	imports: [
		RouterLink,
		Button,
		ErrorState,
		ImagePreview,
		MediaThumbnail,
		PageLoadingSource,
		PageSeoDirective,
		NgIcon,
		HlmBreadcrumbImports,
		HlmCardImports,
		HlmEmptyImports,
		HlmSkeleton,
	],
	templateUrl: './games-feature-detail.html',
	providers: [
		provideIcons({
			matArrowBackRound,
			matArrowForwardRound,
			matCategoryRound,
			matCloudOffRound,
			matHomeRound,
			matLocationOnRound,
			matMapRound,
			matRefreshRound,
		}),
	],
	host: { class: 'mx-auto flex w-full max-w-8xl flex-1 flex-col gap-8 px-4 py-6 sm:gap-10 sm:px-6 sm:py-8' },
})
export class GamesFeatureDetail {
	readonly slug = input.required<string>()

	readonly #options = inject(PublicGamesQueryOptions)
	readonly #pageSeo = inject(PageSeo)

	protected readonly detail = injectQuery(() => this.#options.getDetail(this.slug()))

	protected readonly httpStatusCode = HttpStatusCode
	protected readonly countLabel = formatCount
	protected readonly game = computed(() => this.detail.data())
	protected readonly partial = computed(() => this.detail.isLoading() || this.detail.isPlaceholderData())

	protected readonly mapSkeletons = computed(() =>
		Array.from({ length: this.game()?.mapsCount ?? 2 }, (value, index) => index),
	)

	protected readonly seoDescription = computed(() => {
		const current = this.game()

		if (!current) return null

		if (current.description?.trim()) return current.description.trim()

		return `Explore interactive maps for ${current.name} with ${formatCount(current.mapsCount, 'map')} and ${formatCount(current.markersCount, 'marker')} on Pinveria.`
	})

	protected readonly seoImage = computed(() => {
		const current = this.game()

		if (!current) return null

		return buildGameOgImagePath(current.slug, {
			name: current.name,
			developer: current.developer ?? null,
			mapsCount: current.mapsCount,
			markersCount: current.markersCount,
			coverUrl: current.cover.url,
			bannerUrl: current.banner?.url ?? null,
		})
	})

	protected readonly seoJsonLd = computed(() => {
		const current = this.game()

		if (!current) return null

		const gameUrl = this.#pageSeo.resolveAbsoluteUrl(`/games/${current.slug}`)
		const homeUrl = this.#pageSeo.resolveAbsoluteUrl('/')
		const imageAsset = current.banner ?? current.cover
		const imageUrl = this.#pageSeo.resolveAbsoluteUrl(imageAsset.url)
		const description = this.seoDescription()

		const videoGame: Record<string, unknown> = {
			'@context': 'https://schema.org',
			'@type': 'VideoGame',
			name: current.name,
			url: gameUrl,
			image: imageUrl,
		}

		if (description) {
			videoGame['description'] = description
		}

		if (current.developer) {
			videoGame['author'] = { '@type': 'Organization', name: current.developer }
		}

		if (current.releaseDate) {
			videoGame['datePublished'] = current.releaseDate
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
					name: current.name,
					item: gameUrl,
				},
			],
		}

		return [videoGame, breadcrumb]
	})
}
