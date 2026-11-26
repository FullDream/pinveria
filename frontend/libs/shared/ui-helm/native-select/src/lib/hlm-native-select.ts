import type { BooleanInput } from '@angular/cdk/coercion'
import {
	booleanAttribute,
	ChangeDetectionStrategy,
	Component,
	computed,
	forwardRef,
	inject,
	input,
	linkedSignal,
	output,
} from '@angular/core'
import { NG_VALUE_ACCESSOR, type ControlValueAccessor } from '@angular/forms'
import { NgIcon, provideIcons } from '@ng-icons/core'
import { lucideChevronDown } from '@ng-icons/lucide'
import { BrnFieldControl, provideBrnLabelable } from '@spartan-ng/brain/field'
import { type ChangeFn, type TouchFn } from '@spartan-ng/brain/forms'
import { classes, hlm } from '@spartan-ng/helm/utils'
import type { ClassValue } from 'clsx'

export const HLM_NATIVE_SELECT_VALUE_ACCESSOR = {
	provide: NG_VALUE_ACCESSOR,
	useExisting: forwardRef(() => HlmNativeSelect),
	multi: true,
}

let nextId = 0

@Component({
	selector: 'hlm-native-select',
	exportAs: 'hlmNativeSelect',
	imports: [NgIcon],
	providers: [
		HLM_NATIVE_SELECT_VALUE_ACCESSOR,
		provideIcons({ lucideChevronDown }),
		provideBrnLabelable(HlmNativeSelect),
	],
	changeDetection: ChangeDetectionStrategy.OnPush,
	hostDirectives: [BrnFieldControl],
	host: {
		'data-slot': 'native-select-wrapper',
		'[attr.data-size]': 'size()',
	},
	template: `
		<select
			data-slot="native-select"
			[id]="selectId()"
			[class]="computedSelectClass()"
			[attr.data-size]="size()"
			[attr.aria-invalid]="ariaInvalid() ? 'true' : null"
			[attr.data-invalid]="ariaInvalid() ? 'true' : null"
			[attr.data-dirty]="dirty?.() ? 'true' : null"
			[attr.data-touched]="touched?.() ? 'true' : null"
			[attr.data-matches-spartan-invalid]="spartanInvalid() ? 'true' : null"
			[value]="value()"
			[disabled]="internalDisabled()"
			(change)="valueChanged($event)"
			(blur)="blur()"
		>
			<ng-content />
		</select>

		<ng-icon
			name="lucideChevronDown"
			[class]="computedSelectIconClass()"
			aria-hidden="true"
			data-slot="native-select-icon"
		/>
	`,
})
export class HlmNativeSelect implements ControlValueAccessor {
	readonly #fieldControl = inject(BrnFieldControl, { optional: true })

	public readonly selectId = input<string>(`hlm-native-select-${nextId++}`)

	public readonly selectClass = input<ClassValue>('')

	protected readonly computedSelectClass = computed(() =>
		hlm(
			'border-input bg-input/30 placeholder:text-muted-foreground selection:bg-primary selection:text-primary-foreground dark:bg-input/30 dark:hover:bg-input/50 focus-visible:border-ring focus-visible:ring-ring/50 data-[matches-spartan-invalid=true]:ring-destructive/20 dark:data-[matches-spartan-invalid=true]:ring-destructive/40 data-[matches-spartan-invalid=true]:border-destructive dark:data-[matches-spartan-invalid=true]:border-destructive/50 h-9 min-w-0 rounded-4xl py-1 ps-3 pe-8 text-sm data-[size=sm]:h-8 w-full appearance-none border transition-colors outline-none select-none focus-visible:ring-3 disabled:pointer-events-none disabled:cursor-not-allowed data-[matches-spartan-invalid=true]:ring-3',
			this.selectClass(),
		),
	)

	public readonly selectIconClass = input<ClassValue>('')

	protected readonly computedSelectIconClass = computed(() =>
		hlm(
			'text-muted-foreground end-3.5 text-[length:--spacing(4)] pointer-events-none absolute top-1/2 -translate-y-1/2 select-none',
			this.selectIconClass(),
		),
	)

	public readonly size = input<'sm' | 'default'>('default')

	public readonly disabled = input<boolean, BooleanInput>(false, { transform: booleanAttribute })

	protected readonly internalDisabled = linkedSignal(this.disabled)

	/** Whether to force the input into an invalid state. */
	public readonly forceInvalid = input<boolean, BooleanInput>(false, { transform: booleanAttribute })

	/** Manual override for aria-invalid. When not set, auto-detects from the parent autocomplete error state. */
	public readonly ariaInvalidInput = input<boolean | undefined, BooleanInput>(undefined, {
		transform: (v: BooleanInput) => (v === '' || v === undefined ? undefined : booleanAttribute(v)),
		alias: 'aria-invalid',
	})

	protected readonly ariaInvalid = computed(() => this.ariaInvalidInput() ?? this.invalid?.())

	public readonly valueInput = input<string | undefined | null>('', { alias: 'value' })
	public readonly value = linkedSignal(this.valueInput)

	public readonly valueChange = output<string | undefined | null>()

	protected onChange?: ChangeFn<string | undefined | null>
	protected onTouched?: TouchFn

	public readonly labelableId = this.selectId

	protected readonly invalid = this.#fieldControl?.invalid
	protected readonly touched = this.#fieldControl?.touched
	protected readonly dirty = this.#fieldControl?.dirty
	protected readonly spartanInvalid = computed(() => this.forceInvalid() || this.#fieldControl?.spartanInvalid())

	constructor() {
		classes(() => 'group/native-select relative w-fit has-[select:disabled]:opacity-50')
	}

	protected valueChanged(event: Event): void {
		const value = (event.target as HTMLSelectElement).value

		this.value.set(value)
		this.valueChange.emit(value)
		this.onChange?.(value)
		this.onTouched?.()
	}

	protected blur(): void {
		this.onTouched?.()
	}

	/** CONTROL VALUE ACCESSOR */
	public writeValue(value: string | undefined | null): void {
		this.value.set(value)
	}

	public registerOnChange(fn: ChangeFn<string | undefined | null>): void {
		this.onChange = fn
	}

	public registerOnTouched(fn: TouchFn): void {
		this.onTouched = fn
	}

	public setDisabledState(isDisabled: boolean): void {
		this.internalDisabled.set(isDisabled)
	}
}
