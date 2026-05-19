/**
 * AdminTrainers.jsx — lista de todos os Personal Trainers com acções de administração.
 *
 * Acções disponíveis por Personal Trainer:
 *   - Suspender / Reativar (bloqueia/desbloqueia acesso)
 *   - Conceder / Revogar isenção de billing (free-forever)
 */

import { useEffect, useState, useCallback } from 'react';
import { toast } from 'react-toastify';
import { Shield, ShieldOff, Ban, CheckCircle, RefreshCw } from 'lucide-react';
import { getTrainers, suspendTrainer, activateTrainer, grantExemption, revokeExemption } from '../../api/adminApi';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Card, CardContent } from '../../components/ui/card';

// Mapeia status de subscrição para variante do Badge
function getStatusVariant(status) {
    if (status === 'active')        return 'default';
    if (status === 'trialing')      return 'secondary';
    if (status === 'past_due')     return 'warning';
    if (status === 'cancelled')    return 'destructive';
    if (status == 'trial_expired') return 'destructive';
    return 'outline';
}

// Label légivel para o status de subscrição
function getStatusLabel(status) {
    const labels = {
        active: 'Ativo',
        trialing: 'Trial',
        past_due: 'Atrasado',
        cancelled: 'Cancelado',
        trial_expired: 'Trial Expirado',
    };
    return labels[status] ?? status ?? '—';
}

export default function AdminTrainers() {
    const [trainers, setTrainers] = useState([]);
    const [loading, setLoading] = useState(true);
    // loadingId regista o ID do Personal Trainer cuja acção está em progresso, para mostrar spinner apenas nesse botão específico
    const [loadingId, setLoadingId] = useState(null); 

    const fetchTrainers = useCallback(async () => {
        try {
            setLoading(true);
            const data = await getTrainers();
            setTrainers(data);
        } catch {
            toast.error('Erro ao carregar lista de Personal Trainers.');
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        fetchTrainers();
    }, [fetchTrainers]);

    // Executa uma acção e recarrega a lista no final
    async function handleAction(trainerId, action,  successMsg) {
        setLoadingId(trainerId);
        try {
            await action(trainerId);
            toast.success(successMsg);
            await fetchTrainers();
        } catch (error) {
            toast.error(error?.response?.data?.detail || 'Erro ao executar acção.');
        } finally {
            setLoadingId(null);
        }
    }

    if (loading) {
        return (
            <div className="p-6 flex items-center justify-center min-h-64">
                <div className="h-6 w-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
            </div>
        );
    }

    return (
        <div className="p-4 lg:p-6 flex flex-col gap-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-semibold text-foreground">Personal Trainers</h1>
                    <p className="text-sm text-muted-foreground mt-1">
                        {trainers.length} Personal Trainer{trainers.length !== 1 ? 's' : ''} registado{trainers.length !== 1 ? 's' : ''} na plataforma
                    </p>
                </div>
                <Button variant="outline" size="sm" onClick={fetchTrainers}>
                    <RefreshCw className="h-4 w-4 mr-2" />
                    Atualizar
                </Button>
            </div>

            {/* Tabela Descktop */}
            <div className="hidden md:block rounded-lg border border-border overflow-hidden">
                <table className="w-full text-sm">
                    <thead className="bg-muted/50">
                        <tr>
                            <th className="text-left px-4 py-3 font-medium text-foreground">Personal Trainer</th>
                            <th className="text-left px-4 py-3 font-medium text-foreground">Subscrição</th>
                            <th className="text-left px-4 py-3 font-medium text-foreground">Tier</th>
                            <th className="text-left px-4 py-3 font-medium text-foreground">Clientes</th>
                            <th className="text-left px-4 py-3 font-medium text-foreground">Ações</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-border">
                        {trainers.map(trainer => (
                            <tr key={trainer.user_id} className="bg-card hover:bg-muted/30 transition-colors">
                                <td className="px-4 py-3">
                                    <div>
                                        <p className="font-medium text-foreground">{trainer.full_name}</p>
                                        <p className="text-xs text-muted-foreground">{trainer.email}</p>
                                    </div>
                                    <div className="flex gap-1 mt-1">
                                        {!trainer.is_active && (
                                            <Badge variant="destructive" className="text-sm">Suspenso</Badge>
                                        )}
                                        {trainer.is_exempt_from_billing && (
                                            <Badge variant="secondary" className="text-sm">Isento</Badge>
                                        )}
                                    </div>
                                </td>
                                <td className="px-4 py-3">
                                    <Badge variant={getStatusVariant(trainer.subscription_status)}>
                                        {getStatusLabel(trainer.subscription_status)}
                                    </Badge>
                                </td>
                                <td className="px-4 py-3 capitalize text-foreground">
                                    {trainer.subscription_tier ?? '—'}
                                </td>
                                <td className="px-4 py-3 text-right text-foreground">
                                    {trainer.active_clients_count}
                                </td>
                                <td className="px-4 py-3">
                                    <div className="flex justify-end gap-2">
                                        {/* Suspender / Reativar */}
                                        {trainer.is_active ? (
                                            <Button
                                                variant="outline"
                                                size="sm"
                                                disabled={loadingId === trainer.user_id}
                                                onClick={() => handleAction(trainer.user_id, suspendTrainer, `${trainer.full_name} suspenso.`)}
                                                className="text-destructive hover:bg-destructive"
                                            >
                                                <Ban className="h-3.5 w-3.5 mr-1" />
                                                Suspender
                                            </Button>
                                        ) : (
                                            <Button
                                                variant="outline"
                                                size="sm"
                                                disabled={loadingId === trainer.user_id}
                                                onClick={() => handleAction(trainer.user_id, activateTrainer, `${trainer.full_name} reativado.`)}
                                                className="text-success hover:bg-success"
                                            >
                                                <CheckCircle className="h-3.5 w-3.5 mr-1" />
                                                Reativar
                                            </Button>
                                        )}
                                        {/* Conceder / Revogar Isenção */}
                                        {trainer.is_exempt_from_billing ? (
                                            <Button
                                                variant="outline"
                                                size="sm"
                                                disabled={loadingId === trainer.user_id}
                                                onClick={() => handleAction(trainer.user_id, revokeExemption, 'Isenção revogada.')}
                                                className="text-success hover:bg-success"
                                            >
                                                <ShieldOff className="h-3.5 w-3.5 mr-1" />
                                                Revogar Isenção
                                            </Button>
                                        ) : (
                                            <Button
                                                variant="outline"
                                                size="sm"
                                                disabled={loadingId === trainer.user_id}
                                                onClick={() => handleAction(trainer.user_id, grantExemption, 'Isenção concedida.')}
                                            >
                                                <Shield className="h-3.5 w-3.5 mr-1" />
                                                Conceder Isenção
                                            </Button>
                                        )}
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            {/* Card Mobile */}
            <div className="flex flex-col gap-3 md:hidden">
                {trainers.map(trainer => (
                    <Card key={trainer.user_id} className="border-border bg-card">
                        <CardContent className="p-4 flex flex-col gap-3">
                            <div className="flex items-center justify-between">
                                <div>
                                    <p className="font-medium text-foreground">{trainer.full_name}</p>
                                    <p className="text-xs text-muted-foreground">{trainer.email}</p>
                                </div>
                                <Badge variant={getStatusVariant(trainer.subscription_status)}>
                                    {getStatusLabel(trainer.subscription_status)}
                                </Badge>
                            </div>
                            <div className="flex gap-2 flex-wrap">
                                {!trainer.is_active && <Badge variant="destructive">Suspenso</Badge>}
                                {trainer.is_exempt_from_billing && <Badge variant="secondary">Isento</Badge>}
                                <span className="text-xs text-muted-foreground">
                                    {trainer.active_clients_count} clientes · {trainer.subscription_tier ?? '—'}
                                </span>
                            </div>
                            <div className="flex gap-2 flex-wrap">
                                <Button
                                    variant="outline"
                                    size="sm"
                                    disabled={loadingId === trainer.user_id}
                                    onClick={() => handleAction(
                                        trainer.user_id,
                                        trainer.is_active ? suspendTrainer : activateTrainer,
                                        trainer.is_active ? `${trainer.full_name} suspenso.` : `${trainer.full_name} reativado.`
                                    )}
                                    className={trainer.is_active ? 'text-destructive hover:bg-destructive' : ''}
                                >
                                    {trainer.is_active ? <><Ban className="h-3.5 w-3.5 mr-1" /> Suspender</> : <><CheckCircle className="h-3.5 w-3.5 mr-1" /> Reativar</>}
                                </Button>
                                <Button
                                    variant="outline"
                                    size="sm"
                                    disabled={loadingId === trainer.user_id}
                                    onClick={() => handleAction(
                                        trainer.user_id,
                                        trainer.is_exempt_from_billing ? revokeExemption : grantExemption,
                                        trainer.is_exempt_from_billing ? 'Isenção revogada.' : 'Isenção concedida.'
                                    )}
                                >
                                    {trainer.is_exempt_from_billing
                                        ? <><ShieldOff className="h-3.5 w-3.5 mr-1" /> Revogar Isenção</>
                                        : <><Shield className="h-3.5 w-3.5 mr-1" /> Conceder Isenção</>}
                                </Button>
                            </div>
                        </CardContent>
                    </Card>
                ))}
            </div>
        </div>
    );
}
