import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { emailRules, fullNameRules, heightRules } from '@/utils/validators';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectTrigger,
  SelectValue,
  SelectItem,
} from '@/components/ui/select';

// ─── Prefixos telefónicos mais comuns ──────────────────────────────────────
// Lista contém: código ISO do país, nome em PT-PT, prefixo e flag emoji.
// +351 (Portugal) aparece primeiro e é o default.
const PHONE_PREFIXES = [
  { code: 'PT', label: 'Portugal', prefix: '+351', flag: '🇵🇹' },
  { code: 'BR', label: 'Brasil', prefix: '+55', flag: '🇧🇷' },
  { code: 'ES', label: 'Espanha', prefix: '+34', flag: '🇪🇸' },
  { code: 'FR', label: 'França', prefix: '+33', flag: '🇫🇷' },
  { code: 'DE', label: 'Alemanha', prefix: '+49', flag: '🇩🇪' },
  { code: 'GB', label: 'Reino Unido', prefix: '+44', flag: '🇬🇧' },
  { code: 'US', label: 'EUA', prefix: '+1', flag: '🇺🇸' },
  { code: 'AO', label: 'Angola', prefix: '+244', flag: '🇦🇴' },
  { code: 'MZ', label: 'Moçambique', prefix: '+258', flag: '🇲🇿' },
  { code: 'CV', label: 'Cabo Verde', prefix: '+238', flag: '🇨🇻' },
  { code: 'CH', label: 'Suíça', prefix: '+41', flag: '🇨🇭' },
  { code: 'LU', label: 'Luxemburgo', prefix: '+352', flag: '🇱🇺' },
  { code: 'NL', label: 'Países Baixos', prefix: '+31', flag: '🇳🇱' },
  { code: 'IT', label: 'Itália', prefix: '+39', flag: '🇮🇹' },
  { code: 'BE', label: 'Bélgica', prefix: '+32', flag: '🇧🇪' },
];

// ─── Componente de input de telefone com prefixo ───────────────────────────
/**
 * Renderiza um dropdown de prefixo + input de número lado a lado.
 * O estado é controlado externamente via `prefix`/`onPrefixChange` e
 * `number`/`onNumberChange` para integrar com react-hook-form do pai.
 */

function PhoneInput({
  prefix,
  onPrefixChange,
  number,
  onNumberChange,
  error,
  id,
  placeholder,
}) {
  return (
    <div className="flex gap-2">
      {/* Dropdown de prefixo — largura fixa */}
      <Select value={prefix} onValueChange={onPrefixChange}>
        <SelectTrigger className="bg-background border-input text-foreground w-27.5 shrink-0">
          <SelectValue />
        </SelectTrigger>
        <SelectContent className="bg-popover border-border max-h-60">
          {PHONE_PREFIXES.map((p) => (
            <SelectItem key={p.code} value={p.prefix}>
              <span className="flex items-center gap-2">
                <span>{p.flag}</span>
                <span className="text-muted-foreground text-xs">
                  {p.prefix}
                </span>
                <span className="hidden sm:inline text-xs truncate max-w-20">
                  {p.label}
                </span>
              </span>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {/* Campo do número */}
      <div className="flex-1">
        <Input
          id={id}
          type="tel"
          value={number}
          onChange={(e) => {
            // Apenas dígitos, espaços e traços
            const cleaned = e.target.value.replace(/[^0-9\s\-]/g, '');
            onNumberChange(cleaned);
          }}
          className="bg-background border-input text-foreground"
          placeholder={placeholder || '9XX XXX XXX'}
        />
        {error && <p className="text-xs text-destructive mt-1">{error}</p>}
      </div>
    </div>
  );
}

/**
 * Dialog model para criar ou editar um cliente
 *
 * Props:
 * register: liga inputs ao form
 * handleSubmit: função para lidar com submissão do form
 * -formState: contém erros de validação
 * -reset: função para resetar o form
 * -setValue: função para setar valores do form (usada para editar)
 *
 * @param {Object} open - Controla se o dialog está aberto
 * @param {Function} onOpenChange - Função chamada quando o estado de abertura muda
 * @param {Object|null} client - se preenchido, dialog funciona como edição. Se null, funciona como criação
 * @param {Function} inSave - Callback com os dados do formulário quando o usuário submete
 */
export default function ClientFormDialog({
  open,
  onOpenChange,
  client,
  onSave,
}) {
  const isEditing = !!client;

  // Estado do prefixo separado do react-hook-form (Radix Select não suporta register)
  const [phonePrefix, setPhonePrefix] = useState('+351');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [emergencyPrefix, setEmergencyPrefix] = useState('+351');
  const [emergencyNumber, setEmergencyNumber] = useState('');

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      full_name: '',
      phone: '',
      email: '',
      birth_date: '',
      sex: '',
      height_cm: '',
      objetive: '',
      notes: '',
      emergency_contact_name: '',
      emergency_contact_phone: '',
    },
  });

  useEffect(() => {
    if (!open) return; // Só executa quando o dialog é aberto

    if (client) {
      // Modo edição: separar prefixo do número guardado
      const splitPhone = (fullPhone = '') => {
        const match = PHONE_PREFIXES.find((p) =>
          fullPhone.startsWith(p.prefix)
        );
        return match
          ? {
              prefix: match.prefix,
              number: fullPhone.slice(match.prefix.length).trim(),
            }
          : { prefix: '+351', number: fullPhone }; // default para +351 se não reconhecer
      };

      const { prefix: pp, number: pn } = splitPhone(client.phone || '');
      const { prefix: ep, number: en } = splitPhone(
        client.emergency_contact_phone || ''
      );

      setPhonePrefix(pp);
      setPhoneNumber(pn);
      setEmergencyPrefix(ep);
      setEmergencyNumber(en);

      // Modo edição: preenche o form com os dados do cliente
      reset({
        full_name: client.full_name || '',
        phone: client.phone || '',
        email: client.email || '',
        birth_date: client.birth_date || '',
        sex: client.sex || '',
        height_cm: client.height_cm || '',
        training_modality: client.training_modality || 'presencial',
        objetive: client.objetive || '',
        notes: client.notes || '',
        emergency_contact_name: client.emergency_contact_name || '',
        emergency_contact_phone: client.emergency_contact_phone || '',
      });
    } else {
      // Modo criação: valores por omissão
      setPhonePrefix('+351');
      setPhoneNumber('');
      setEmergencyPrefix('+351');
      setEmergencyNumber('');

      reset({
        full_name: '',
        email: '',
        birth_date: '',
        sex: '',
        height_cm: '',
        training_modality: 'presencial',
        objetive: '',
        notes: '',
        emergency_contact_name: '',
        emergency_contact_phone: '',
      });
    }
  }, [open, client, reset]);

  /**
   * Processa os dados antes de enviar:
   * - Converte height_cm para número
   * -Converte campos vazios para null (para o backend entender que são opcionais)
   */

  const onSubmit = (data) => {
    // Combina prefixo + número antes de enviar ao backend
    // ex: "+351" + "912345678" => "+351912345678"
    const buildPhone = (prefix, number) => {
      const num = number.replace(/\s/g, ''); // remove espaços para validação
      return num ? `${prefix}${num}` : '';
    };

    const cleaned = {
      ...data,
      phone: buildPhone(phonePrefix, phoneNumber),
      sex: data.sex || null,
      height_cm: data.height_cm ? Number(data.height_cm) : null,
      training_modality: data.training_modality || 'presencial',
      objetive: data.objetive || null,
      notes: data.notes || null,
      emergency_contact_name: data.emergency_contact_name || null,
      emergency_contact_phone:
        buildPhone(emergencyPrefix, emergencyNumber) || null,
    };

    onSave(cleaned);
  };

  //opções de sexo para o select
  const sexOptions = [
    { value: 'male', label: 'Masculino' },
    { value: 'female', label: 'Feminino' },
    { value: 'other', label: 'Outro' },
    { value: 'unknown', label: 'Desconhecido' },
  ];

  const modalityOptions = [
    { value: 'presencial', label: 'Presencial' },
    { value: 'online', label: 'Online' },
    { value: 'hibrido', label: 'Híbrido' },
  ];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="bg-card border-border text-foreground sm:max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="text-foreground">
            {isEditing ? 'Editar Cliente' : 'Criar Cliente'}
          </DialogTitle>
          <DialogDescription className="text-muted-foreground">
            {isEditing
              ? 'Atualiza as informações do cliente.'
              : 'Preenche os dados para criar um novo cliente.'}
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit(onSubmit)}
          className="flex flex-col gap-4 mt-2"
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {/* Nome completo — ocupa 2 colunas */}
            <div className="flex flex-col gap-1.5 sm:col-span-2">
              <Label htmlFor="full_name">
                Nome Completo <span className="text-destructive">*</span>
              </Label>
              <Input
                id="full_name"
                {...register('full_name', fullNameRules)}
                className="bg-background border-input text-foreground"
                placeholder="Nome completo do cliente"
              />
              {errors.full_name && (
                <span className="text-xs text-destructive">
                  {errors.full_name.message}
                </span>
              )}
            </div>

            {/* Telemóvel — prefixo + número */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="phone">
                Telemóvel <span className="text-destructive">*</span>
              </Label>
              <PhoneInput
                id="phone"
                prefix={phonePrefix}
                onPrefixChange={setPhonePrefix}
                number={phoneNumber}
                onNumberChange={setPhoneNumber}
                error={
                  !phoneNumber.trim() && errors.phone
                    ? 'Telemóvel obrigatório'
                    : undefined
                }
                placeholder="9XX XXX XXX"
              />
            </div>

            {/* Email */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="email">
                Email <span className="text-destructive">*</span>
              </Label>
              <Input
                id="email"
                type="email"
                {...register('email', emailRules)}
                className="bg-background border-input text-foreground"
                placeholder="email@exemplo.com"
              />
              {errors.email && (
                <span className="text-xs text-destructive">
                  {errors.email.message}
                </span>
              )}
            </div>

            {/* Data de nascimento */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="birth_date">
                Data de Nascimento <span className="text-destructive">*</span>
              </Label>
              <Input
                id="birth_date"
                type="date"
                {...register('birth_date', {
                  required: 'Data de nascimento obrigatória',
                })}
                className="bg-background border-input text-foreground"
              />
              {errors.birth_date && (
                <span className="text-xs text-destructive">
                  {errors.birth_date.message}
                </span>
              )}
            </div>

            {/* Sexo */}
            <div className="flex flex-col gap-1.5">
              <Label>Sexo</Label>
              <Select
                value={watch('sex') || ''}
                onValueChange={(val) => setValue('sex', val)}
              >
                <SelectTrigger className="bg-background border-input text-foreground">
                  <SelectValue placeholder="Selecciona o sexo" />
                </SelectTrigger>
                <SelectContent className="bg-popover border-border">
                  {sexOptions.map((opt) => (
                    <SelectItem key={opt.value} value={opt.value}>
                      {opt.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Altura */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="height_cm">Altura (cm)</Label>
              <Input
                id="height_cm"
                type="number"
                {...register('height_cm', heightRules)}
                className="bg-background border-input text-foreground"
                placeholder="175"
              />
              {errors.height_cm && (
                <span className="text-xs text-destructive">
                  {errors.height_cm.message}
                </span>
              )}
            </div>

            {/* Modalidade de treino — campo que faltava */}
            <div className="flex flex-col gap-1.5">
              <Label>
                Modalidade de Treino <span className="text-destructive">*</span>
              </Label>
              <Select
                value={watch('training_modality') || 'presencial'}
                onValueChange={(val) => setValue('training_modality', val)}
              >
                <SelectTrigger className="bg-background border-input text-foreground">
                  <SelectValue placeholder="Selecciona a modalidade" />
                </SelectTrigger>
                <SelectContent className="bg-popover border-border">
                  {modalityOptions.map((opt) => (
                    <SelectItem key={opt.value} value={opt.value}>
                      {opt.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Objetivo — ocupa 2 colunas */}
            <div className="flex flex-col gap-1.5 sm:col-span-2">
              <Label htmlFor="objetive">Objetivo</Label>
              <Input
                id="objetive"
                {...register('objetive')}
                className="bg-background border-input text-foreground"
                placeholder="Ex: Perda de peso, ganho de massa muscular..."
              />
            </div>

            {/* Notas — ocupa 2 colunas */}
            <div className="flex flex-col gap-1.5 sm:col-span-2">
              <Label htmlFor="notes">Notas</Label>
              <Textarea
                id="notes"
                {...register('notes')}
                className="bg-background border-input text-foreground resize-none"
                placeholder="Notas adicionais sobre o cliente"
                rows={2}
              />
            </div>

            {/* Contacto de emergência — nome */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="emergency_contact_name">Emergência — Nome</Label>
              <Input
                id="emergency_contact_name"
                {...register('emergency_contact_name')}
                className="bg-background border-input text-foreground"
                placeholder="Nome do contacto de emergência"
              />
            </div>

            {/* Contacto de emergência — telefone com prefixo */}
            <div className="flex flex-col gap-1.5">
              <Label>Emergência — Telefone</Label>
              <PhoneInput
                id="emergency_phone"
                prefix={emergencyPrefix}
                onPrefixChange={setEmergencyPrefix}
                number={emergencyNumber}
                onNumberChange={setEmergencyNumber}
                placeholder="9XX XXX XXX"
              />
            </div>
          </div>

          {/* Botões */}
          <div className="flex justify-end gap-3 pt-2">
            <Button
              type="button"
              variant="ghost"
              onClick={() => onOpenChange(false)}
              className="text-muted-foreground hover:text-foreground"
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              disabled={isSubmitting}
              className="bg-primary text-primary-foreground hover:bg-primary/90"
            >
              {isEditing ? 'Guardar Alterações' : 'Criar Cliente'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
