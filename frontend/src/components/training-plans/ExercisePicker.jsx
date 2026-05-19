import { useState } from 'react';
import { matchesSearch } from '@/utils/validators';
import { useExercises } from '@/hooks/useExercises';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Search, Dumbbell, Plus } from 'lucide-react';

/**
 * Dialog para selecionar um exercício da biblioteca e adicionar a um dia do plano.
 *
 * Fluxo:
 * 1. Utilizador clica "Adicionar Exercício" num dia
 * 2. Este dialog abre com a lista de todos os exercícios
 * 3. Utilizador pesquisa e clica num exercício
 * 4. onSelect é chamada com o exercício escolhido
 * 5. O pai (PlanDaysList) fecha o dialog e faz POST à API
 *
 * @param {boolean} open
 * @param {Function} onOpenChange
 * @param {Function} onSelect - Chamada com o objeto do exercício selecionado
 */

export default function ExercisePicker({ open, onOpenChange, onSelect }) {
  const [search, setSearch] = useState('');

  // Busca todos os exercicios ativos
  // useExercises é um hook customizado que faz fetch à API e retorna {exercises, loading, error}
  const { exercises, loading, error } = useExercises({ only_active: true });

  // Filtro local por texto
  // A API também suporta ?q= mas filtrar localmente evita chamadas extras
  const filtered = exercises.filter((ex) => matchesSearch(search, ex.name));

  const handleSelect = (exercise) => {
    onSelect(exercise);
    onOpenChange(false); // fecha o dialog
    setSearch(''); // limpa o campo de pesquisa para a próxima abertura
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="bg-card border-border text-foreground sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Dumbbell className="h-5 w-5 text-primary" />
            Selecionar Exercício
          </DialogTitle>
          <DialogDescription className="text-muted-foreground">
            Pesquise e escolha um exercício para adicionar ao dia de treino.
          </DialogDescription>
        </DialogHeader>

        {/* Campo de pesquisa */}
        <div className="relative mt-2">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Pesquisar exercícios..."
            className="pl-9 bg-background border-input"
            autoFocus
          />
        </div>

        {/* Lista de exercícios */}
        <div className="mt-2 max-h-80 overflow-y-auto flex flex-col gap-1 pr-1">
          {loading ? (
            //Skeleton enquanto carrega
            [1, 2, 3, 4, 5].map((i) => (
              <div key={i} className="h-12 rounded-lg bg-muted animate-pulse" />
            ))
          ) : filtered.length === 0 ? (
            <p className="text-center text-muted-foreground py-8 text-sm">
              Nenhum exercício encontrado.
            </p>
          ) : (
            filtered.map((exercise) => (
              <button
                key={exercise.id}
                type="button"
                onClick={() => handleSelect(exercise)}
                className="flex items-center gap-3 rounded-lg px-3 py-2.5 text-left hover:bg-accent transition-colors w-full"
              >
                {/* Ícone */}
                <div className="h-8 w-8 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
                  <Dumbbell className="h-4 w-4 text-primary" />
                </div>

                {/* Nome e músculos do exercício */}
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-foreground truncate">
                    {exercise.name}
                  </p>
                  {/* muscles pode ser array ou string - tratamos ambos */}
                  {exercise.muscles && (
                    <p className="text-xs text-muted-foreground truncate">
                      {Array.isArray(exercise.muscles)
                        ? exercise.muscles.join(', ')
                        : exercise.muscles}
                    </p>
                  )}
                </div>

                {/* Botão de adicionar */}
                <Plus className="h-4 w-4 text-muted-foreground shrink-0" />
              </button>
            ))
          )}
        </div>

        {/* Rodapé com contagem*/}
        <div className="flex items-center justify-between pt-2 border-t border-border">
          <span className="text-xs text-muted-foreground">
            {filtered.length} exercício{filtered.length !== 1 ? 's' : ''}
          </span>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onOpenChange(false)}
            className="text-muted-foreground"
          >
            Cancelar
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
