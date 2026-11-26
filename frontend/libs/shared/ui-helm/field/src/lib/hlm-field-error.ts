import { BooleanInput } from '@angular/cdk/coercion'
import {
	booleanAttribute,
	ChangeDetectionStrategy,
	Component,
	computed,
	effect,
	EffectRef,
	inject,
	input,
	OnDestroy,
} from '@angular/core'
import { BrnField, BrnFieldA11yService } from '@spartan-ng/brain/field'
import { classes } from '@spartan-ng/helm/utils'

let nextId = 0

@Component({
	selector: 'hlm-field-error',
	exportAs: 'hlmFieldError',
	changeDetection: ChangeDetectionStrategy.OnPush,
	host: {
		role: 'alert',
		'data-slot': 'field-error',
		'[attr.id]': 'id()',
		'[hidden]': '!display()',
	},
	template: `
		@if (display()) {
			<ng-content />
		}
	`,
})
export class HlmFieldError implements OnDestroy {
	readonly #field = inject(BrnField, { optional: true })
	readonly #a11y = inject(BrnFieldA11yService, { optional: true, host: true })

	#registeredId?: string | undefined

	readonly #hasParentField = !!this.#field

	/** The unique ID for the field error. If none is supplied, it will be auto-generated. */
	public readonly id = input<string>(`hlm-field-error-${nextId++}`)

	/**
	 * The name of the specific validator error key to match (e.g. 'required').
	 * When omitted, the error is shown if any validation error is present.
	 */
	public readonly validator = input<string>()

	/** Forces the error message to be visible regardless of the control's validation state. */
	public readonly forceShow = input<boolean, BooleanInput>(false, { transform: booleanAttribute })

	protected readonly display = computed(() => !this.#hasParentField || this.forceShow() || this.hasError())

	protected readonly hasError = computed(() => {
		const errors = this.#field?.errors()

		if (!errors) return false

		const validator = this.validator()
		const spartanInvalid = this.#field?.controlState()?.spartanInvalid

		if (!spartanInvalid) return false

		return validator ? validator in errors : Object.keys(errors).length > 0
	})

	readonly #cleanup: EffectRef | null = this.#a11y
		? effect(() => {
				const a11y = this.#a11y

				if (!a11y) return

				const id = this.id()
				const hasError = this.hasError()

				if (this.#registeredId && (this.#registeredId !== id || !hasError)) {
					a11y.unregisterError(this.#registeredId)
					this.#registeredId = undefined
				}

				if (hasError && this.#registeredId !== id) {
					a11y.registerError(id)
					this.#registeredId = id
				}
			})
		: null

	constructor() {
		classes(() => 'text-destructive text-sm font-normal')
	}

	ngOnDestroy(): void {
		this.#cleanup?.destroy()

		if (this.#registeredId) {
			this.#a11y?.unregisterError(this.#registeredId)
		}
	}
}
