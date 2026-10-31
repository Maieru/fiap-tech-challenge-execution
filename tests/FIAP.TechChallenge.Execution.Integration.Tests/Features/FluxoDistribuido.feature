# language: pt
@EndToEnd
Funcionalidade: Fluxo distribuído entre OS e Execution
  A oficina acompanha diagnóstico, autorização e compensação sem compartilhar bancos.

  Cenário: Autorizar e concluir a execução preservando o histórico
    Dado uma ordem aberta no OS e recebida pelo Execution
    Quando o diagnóstico inclui um serviço e uma peça
    E o operador conclui o diagnóstico e autoriza administrativamente
    E a API de execução reinicia e conclui os serviços
    Então a ordem fica finalizada no OS com histórico e auditoria
    E inativar cliente e veículo não oculta a ordem finalizada

  Cenário: Cancelar enquanto aguarda autorização devolve o estoque
    Dado uma ordem aberta no OS e recebida pelo Execution
    Quando o diagnóstico inclui um serviço e uma peça
    E o operador conclui o diagnóstico e solicita o cancelamento
    Então a ordem fica cancelada e a reserva é devolvida uma única vez
