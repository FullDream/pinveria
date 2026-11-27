import { isPlatformBrowser, NgOptimizedImage } from '@angular/common'
import { Component, computed, effect, inject, linkedSignal, PLATFORM_ID } from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { FormsModule } from '@angular/forms'
import { Router } from '@angular/router'
import {
	GAME_SORT_OPTIONS,
	PublicGamesQueryOptions,
	getGameSortLabel,
	type ListPublicGamesSortBy as GameSortBy,
	type ListPublicGamesSortDirection as SortDirection,
	parseGameSort,
} from '@pinveria/games/data-access'
import { Button, ErrorState, PageLoadingSource, PageSeo, PageSeoDirective } from '@pinveria/shared/ui'
import {
	buildHomeOgImagePath,
	DEFAULT_PAGE_SIZE,
	GAME_CATALOG_DESCRIPTION,
	GAME_MAPS_LABEL,
	MARKER_COMPLETION_LABEL,
	PAGE_SIZES,
} from '@pinveria/shared/util'
import { NgIcon, provideIcons } from '@ng-icons/core'
import {
	matCloseRound,
	matCloudOffRound,
	matLayersRound,
	matLocationOnRound,
	matRefreshRound,
	matSearchRound,
	matSortRound,
	matSportsEsportsRound,
} from '@ng-icons/material-symbols/round'
import { HlmEmptyImports } from '@spartan-ng/helm/empty'
import { HlmInputGroupImports } from '@spartan-ng/helm/input-group'
import { HlmNumberedPaginationQueryParams } from '@spartan-ng/helm/pagination'
import { HlmSelectImports } from '@spartan-ng/helm/select'
import { injectQuery } from '@tanstack/angular-query'
import { linkedQueryParam } from 'ngxtension/linked-query-param'
import { debounceTime, distinctUntilChanged, Subject } from 'rxjs'
import { GameCard } from './game-card/game-card'
import { HERO_ATLAS_IMAGE_SRC, HERO_ATLAS_PLACEHOLDER } from './hero-atlas'

@Component({
	selector: 'games-catalog',
	imports: [
		PageLoadingSource,
		PageSeoDirective,
		NgOptimizedImage,
		FormsModule,
		NgIcon,
		Button,
		ErrorState,
		GameCard,
		HlmEmptyImports,
		HlmInputGroupImports,
		HlmNumberedPaginationQueryParams,
		HlmSelectImports,
	],
	templateUrl: './feature-catalog.html',
	providers: [
		provideIcons({
			matCloseRound,
			matCloudOffRound,
			matLayersRound,
			matLocationOnRound,
			matRefreshRound,
			matSearchRound,
			matSortRound,
			matSportsEsportsRound,
		}),
	],
	host: {
		class: 'flex flex-1 flex-col',
	},
})
export class FeatureCatalog {
	readonly #options = inject(PublicGamesQueryOptions)
	readonly #router = inject(Router)
	readonly #platformId = inject(PLATFORM_ID)
	readonly #pageSeo = inject(PageSeo)

	protected readonly search = linkedQueryParam('search', {
		parse: value => value ?? '',
		stringify: value => value || null,
	})

	protected readonly offset = linkedQueryParam('offset', {
		parse: value => {
			const number = Number(value ?? 0)

			return Number.isSafeInteger(number) && number >= 0 && number <= 2147483647 ? number : 0
		},
		stringify: value => value || null,
	})

	protected readonly limit = linkedQueryParam('limit', {
		parse: value => {
			const number = Number(value ?? DEFAULT_PAGE_SIZE)

			return PAGE_SIZES.includes(number) ? number : DEFAULT_PAGE_SIZE
		},
		stringify: value => (value === DEFAULT_PAGE_SIZE ? null : value),
	})

	protected readonly sortBy = linkedQueryParam<GameSortBy>('sortBy', {
		parse: value => (value === 'createdAt' || value === 'updatedAt' ? value : 'name'),
	})

	protected readonly sortDirection = linkedQueryParam<SortDirection>('sortDirection', {
		parse: value => (value === 'desc' ? 'desc' : 'asc'),
	})

	protected readonly listing = injectQuery(() =>
		this.#options.getList({
			search: this.search(),
			offset: this.offset(),
			limit: this.limit(),
			sortBy: this.sortBy(),
			sortDirection: this.sortDirection(),
		}),
	)

	protected readonly heroImage = HERO_ATLAS_IMAGE_SRC
	protected readonly heroPlaceholder = HERO_ATLAS_PLACEHOLDER
	protected readonly pageSizes = PAGE_SIZES
	protected readonly sortOptions = GAME_SORT_OPTIONS
	protected readonly description = GAME_CATALOG_DESCRIPTION
	protected readonly seoImage = buildHomeOgImagePath()

	protected readonly features = [
		{ icon: 'matLocationOnRound', label: MARKER_COMPLETION_LABEL },
		{ icon: 'matLayersRound', label: GAME_MAPS_LABEL },
	] as const

	protected readonly sortLabel = (value: unknown): string => getGameSortLabel(value)

	readonly #searchInput$ = new Subject<string>()

	protected readonly searchInput = linkedSignal(() => this.search())
	protected readonly skeletons = computed(() => Array.from({ length: this.limit() }, (value, index) => index))
	protected readonly sortSelection = computed(() => `${this.sortBy()}:${this.sortDirection()}`)
	protected readonly total = computed(() => this.listing.data()?.total ?? 0)

	protected readonly hasFilterOrCustomSort = computed(
		() =>
			this.search().trim() !== '' ||
			this.sortBy() !== 'name' ||
			this.sortDirection() !== 'asc' ||
			this.limit() !== DEFAULT_PAGE_SIZE,
	)

	protected readonly isCleanPaginatedPage = computed(() => !this.hasFilterOrCustomSort() && this.offset() > 0)

	protected readonly seoTitle = computed(() => {
		if (!this.isCleanPaginatedPage()) return 'Interactive Game Maps'

		const pageNumber = Math.floor(this.offset() / this.limit()) + 1

		return `Interactive Game Maps — Page ${pageNumber}`
	})

	protected readonly seoCanonical = computed(() => (this.isCleanPaginatedPage() ? `/?offset=${this.offset()}` : '/'))

	protected readonly seoJsonLd = computed(() => {
		const siteUrl = this.#pageSeo.resolveAbsoluteUrl('/')

		return [
			{
				'@context': 'https://schema.org',
				'@type': 'WebSite',
				name: 'Pinveria',
				url: siteUrl,
				description: this.description,
				potentialAction: {
					'@type': 'SearchAction',
					target: `${siteUrl}?search={search_term_string}`,
					'query-input': 'required name=search_term_string',
				},
			},
			{
				'@context': 'https://schema.org',
				'@type': 'Organization',
				name: 'Pinveria',
				url: siteUrl,
			},
		]
	})

	constructor() {
		this.#searchInput$.pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed()).subscribe(value => {
			if (value === this.search()) return

			this.search.set(value)
			this.offset.set(0)
		})

		effect(() => {
			const page = this.listing.data()

			if (
				!isPlatformBrowser(this.#platformId) ||
				!page ||
				this.listing.isPlaceholderData() ||
				this.listing.isFetching() ||
				!this.listing.isSuccess()
			)
				return

			if (page.offset > 0 && page.offset >= page.total) {
				const offset = page.total ? Math.floor((page.total - 1) / page.limit) * page.limit : 0

				void this.#router.navigate([], {
					queryParams: { offset: offset || null },
					queryParamsHandling: 'merge',
					replaceUrl: true,
				})
			}
		})
	}

	protected onSearchInput(value: string): void {
		this.searchInput.set(value)
		this.#searchInput$.next(value)
	}

	protected clearSearch(): void {
		this.searchInput.set('')
		this.#searchInput$.next('')

		if (this.search() !== '') {
			this.search.set('')
			this.offset.set(0)
		}
	}

	protected changeSort(value: unknown): void {
		const parsed = parseGameSort(value)

		if (!parsed) return

		this.sortBy.set(parsed.sortBy)
		this.sortDirection.set(parsed.sortDirection)
		this.offset.set(0)
	}
}
