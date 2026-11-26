import { Directive, effect, EffectRef, inject, input, OnDestroy } from '@angular/core'
import { BrnFieldA11yService } from '@spartan-ng/brain/field'
import { classes } from '@spartan-ng/helm/utils'

let nextId = 0

@Directive({
	selector: '[hlmFieldDescription],hlm-field-description',
	exportAs: 'hlmFieldDescription',
	host: {
		'data-slot': 'field-description',
		'[attr.id]': 'id()',
	},
})
export class HlmFieldDescription implements OnDestroy {
	readonly #a11y = inject(BrnFieldA11yService, { optional: true, host: true })

	public readonly id = input<string>(`hlm-field-description-${nextId++}`)

	#registeredId?: string

	readonly #cleanup: EffectRef | null = this.#a11y
		? effect(() => {
				const a11y = this.#a11y

				if (!a11y) return

				const id = this.id()

				if (this.#registeredId && this.#registeredId !== id) {
					a11y.unregisterDescription(this.#registeredId)
				}

				if (this.#registeredId !== id) {
					a11y.registerDescription(id)
					this.#registeredId = id
				}
			})
		: null

	constructor() {
		classes(() => [
			'text-muted-foreground text-sm [[data-variant=legend]+&]:-mt-1.5 leading-normal font-normal text-start group-has-data-horizontal/field:text-balance',
			'last:mt-0 nth-last-2:-mt-1',
			'[&>a:hover]:text-primary [&>a]:underline [&>a]:underline-offset-4',
		])
	}

	ngOnDestroy(): void {
		this.#cleanup?.destroy()

		if (this.#registeredId) {
			this.#a11y?.unregisterDescription(this.#registeredId)
		}
	}
}
