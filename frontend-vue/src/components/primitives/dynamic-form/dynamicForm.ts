export interface DynamicFormSchema<T> {
  fields: DynamicFormSchemaField<T>[];
}

export interface DynamicFormSchemaField<T> {
  name: keyof T;
  label?: string;
  component: any;
  componentProps?: any;
}
