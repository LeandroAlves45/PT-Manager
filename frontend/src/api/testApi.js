import api from './axiosConfig';

//função de teste para verificar se a API está respondendo
export const testConnection = async () => {
    try {
        //testa endpoint de health check
        const health = await api.get('/api/v1/health');
        console.log('Health check:', health.data);

        //testa endpoint de clientes (precisa de autenticação)
        const clients = await api.get('/api/v1/clients/clients');
        console.log('Clientes:', clients.data);

        return true;
    } catch (err) {
        console.error('Erro ao testar conexão:', err);
        return false;
    }
};

