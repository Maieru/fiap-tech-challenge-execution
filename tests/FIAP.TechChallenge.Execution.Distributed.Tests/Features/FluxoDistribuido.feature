# language: pt
Funcionalidade: Integrar OS e Execution por Kafka
  As duas APIs usam autenticação real, bancos exclusivos e um broker Kafka compartilhado.

  Cenário: Executar uma ordem e projetar sua conclusão no OS
    Dado uma ordem aberta no OS e recebida no Execution
    E um serviço e uma peça com cinco unidades cadastrados no Execution
    Quando concluo o diagnóstico com um serviço e duas peças
    E aprovo a execução pela API OS
    E concluo o serviço e finalizo a execução
    Então as duas APIs apresentam a ordem finalizada com histórico e snapshots preservados
    E as mensagens preservam correlação, causação e trace da autorização

  Cenário: Cancelar e reentregar mensagens sem devolver estoque novamente
    Dado uma ordem com diagnóstico concluído e duas peças reservadas
    Quando solicito o cancelamento pela API OS
    Então as duas APIs apresentam a ordem cancelada e o estoque volta para cinco
    Quando reentrego o cancelamento e sua confirmação pelo Kafka
    Então o estoque e o histórico continuam sem efeitos duplicados

  Cenário: Serializar decisões administrativas contraditórias
    Dado uma ordem com diagnóstico concluído e duas peças reservadas
    Quando envio aprovação e cancelamento simultaneamente para o OS
    Então apenas uma decisão é aceita e publicada
    E as duas APIs convergem para a decisão aceita

  Cenário: Receber abertura pendente após reiniciar o Execution
    Dado um cliente e um veículo ativos no OS
    Quando paro o Execution e abro uma ordem no OS
    Então o OS confirma a abertura sem chamar a API Execution
    Quando reinicio o Execution
    Então a abertura pendente é recebida pelo Execution
