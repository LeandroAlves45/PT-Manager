import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

/**
 * gráfico  de barras para mostrar sessões por dia da semana
 *
 * Recebe um array de sessões e calcula internamente o número de sessões por dia da semana
 *
 * @param {Array} sessions - Array de objetos de sessões, cada objeto deve conter:
 */
export default function WeeklyChart({ sessions }) {
  //Nomes dos dias da semana para exibição
  const weekDays = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

  //Calcula o número de sessões por dia da semana
  const data = weekDays.map((day, index) => {
    const count = sessions.filter((session) => {
      const sessionDate = new Date(session.starts_at);
      return sessionDate.getDay() === index; //0 (Dom) a 6 (Sáb)
    }).length;

    return { day, sessoes: count };
  });

  return (
    <Card className="bg-card border-border">
      <CardHeader>
        <CardTitle className="text-foreground">Sessões esta semana</CardTitle>
      </CardHeader>
      <CardContent>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={data}>
            <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
            <XAxis
              dataKey="day"
              className="text-muted-foreground"
              tick={{ fill: 'currentColor' }}
            />
            <YAxis
              className="text-muted-foreground"
              tick={{ fill: 'currentColor' }}
            />
            <Tooltip
              contentStyle={{
                backgroundColor: 'hsl(var(--card))',
                border: '1px solid hsl(var(--border))',
                borderRadius: '8px',
              }}
            />
            <Bar dataKey="sessoes" fill="#00A8E8" radius={[8, 8, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
}
