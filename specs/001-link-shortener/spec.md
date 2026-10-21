# Feature Specification: Encurtador de links

**Feature Branch**: Not created

**Created**: 2026-10-21

**Status**: Draft

**Input**: User description: Criar um encurtador no qual o criador informa uma URL longa e recebe um link curto para compartilhar. Quem acessa o link curto é redirecionado ao destino, e cada acesso é contado. O criador pode consultar a quantidade de acessos. O código é gerado automaticamente com 7 caracteres alfanuméricos ou escolhido pelo criador com 4 a 20 caracteres, e é único em todo o sistema. A URL de destino deve usar HTTP ou HTTPS e ter no máximo 2048 caracteres. O criador pode definir uma data de expiração; links expirados não redirecionam nem contam acesso. O contador nunca diminui e os links permanecem válidos após reinícios. Cadastro e login, domínio personalizado, relatório geográfico e edição do destino de links existentes estão fora de escopo.

## Clarifications

### Session 2026-10-21

- Q: Qualquer pessoa que conheça o código curto deve poder consultar a contagem, ou a consulta deve exigir um segredo separado entregue ao criador? → A: Qualquer pessoa que conheça o código curto pode consultar a contagem.
- Q: Como o serviço deve limitar a criação automatizada de links, já que não haverá cadastro nem login? → A: Não aplicar controles específicos de frequência ou automação.
- Q: Como conciliar a consulta pública definida com a nova solicitação de uma chave de gerenciamento? → A: Não incluir chave de gerenciamento no escopo atual, pois nenhuma operação protegida foi definida.
- Q: Quais caracteres devem ser considerados ambíguos no alfabeto dos códigos? → A: Excluir 0, 1, I, L, O e suas formas minúsculas; normalizar letras para maiúsculas.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Criar e compartilhar um link curto (Priority: P1)

O criador informa uma URL de destino válida e recebe um link curto pronto para compartilhar. Pode aceitar um código gerado automaticamente ou escolher um código próprio.

**Why this priority**: A criação do link é o ponto de entrada do serviço e permite que o destino longo seja compartilhado por meio de um endereço curto.

**Independent Test**: Enviar uma URL válida sem código e verificar que um link curto é retornado com código alfanumérico de 7 caracteres; repetir escolhendo um código permitido e verificar que ele é usado no link retornado.

**Acceptance Scenarios**:

1. **Given** uma URL HTTP ou HTTPS válida com até 2048 caracteres e sem código escolhido, **When** o criador solicita um link curto, **Then** recebe um link cujo código tem exatamente 7 caracteres alfanuméricos.
2. **Given** uma URL válida e um código próprio de 4 a 20 caracteres alfanuméricos ainda não utilizado, **When** o criador solicita um link curto, **Then** recebe um link com o código escolhido.
3. **Given** uma URL inválida, um protocolo diferente de HTTP ou HTTPS, uma URL com mais de 2048 caracteres ou um código próprio fora dos limites permitidos, **When** o criador solicita um link curto, **Then** a solicitação é rejeitada e nenhum link é criado.
4. **Given** um código próprio já utilizado, **When** o criador tenta usá-lo novamente, **Then** a solicitação é rejeitada sem substituir nem alterar o link existente.
5. **Given** uma data de expiração futura, **When** o criador cria o link, **Then** essa data fica associada ao link; se não informar uma data, o link não tem expiração definida.

### User Story 2 - Acessar o destino e contabilizar visitas (Priority: P1)

Uma pessoa abre um link curto ativo, é encaminhada ao destino e tem esse acesso contabilizado. Links expirados ou inexistentes não encaminham a pessoa ao destino.

**Why this priority**: O encaminhamento é a finalidade principal do link curto; a contagem confiável permite ao criador acompanhar seu uso.

**Independent Test**: Criar um link ativo e acessá-lo repetidamente, conferindo o destino e o aumento correspondente da contagem; repetir com um link expirado e confirmar que não há encaminhamento nem aumento.

**Acceptance Scenarios**:

1. **Given** um link existente e ainda válido, **When** uma pessoa acessa seu código, **Then** é encaminhada à URL de destino e a contagem aumenta em exatamente um acesso.
2. **Given** um link que já atingiu sua data de expiração, **When** uma pessoa acessa seu código, **Then** não é encaminhada ao destino e a contagem permanece inalterada.
3. **Given** um código que não corresponde a nenhum link, **When** uma pessoa tenta acessá-lo, **Then** não é encaminhada a um destino.
4. **Given** vários acessos válidos, inclusive acessos repetidos ao mesmo link, **When** cada acesso é concluído, **Then** cada um é contado uma vez e a contagem nunca diminui.
5. **Given** um link ainda válido antes de uma reinicialização da aplicação, **When** a aplicação volta a funcionar, **Then** o mesmo código continua encaminhando ao mesmo destino e preserva a contagem acumulada.

### User Story 3 - Consultar acessos do link (Priority: P2)

O criador consulta a quantidade acumulada de acessos de um link que criou, usando o código do link para identificá-lo. Qualquer pessoa que conheça o código também pode consultar a contagem.

**Why this priority**: A consulta torna a contagem útil ao criador sem exigir cadastro ou login, ambos fora do escopo definido.

**Independent Test**: Consultar a contagem de um código existente após zero, um e vários acessos válidos e verificar que os totais correspondem aos acessos registrados; consultar um código inexistente e verificar uma resposta de não encontrado.

**Acceptance Scenarios**:

1. **Given** um código existente, **When** o criador consulta as estatísticas do link, **Then** recebe a contagem acumulada de acessos válidos, inclusive zero quando ainda não houve acesso.
2. **Given** um código existente com acessos anteriores, **When** ocorre um novo acesso válido e o criador consulta novamente, **Then** o total apresentado inclui esse acesso e não é menor que o total anterior.
3. **Given** um código inexistente, **When** o criador consulta as estatísticas, **Then** o sistema informa que o link não foi encontrado.

### Edge Cases

- Uma URL com exatamente 2048 caracteres é aceita; uma com 2049 é rejeitada.
- Códigos próprios com exatamente 4 ou 20 caracteres são aceitos; códigos com menos de 4 ou mais de 20 são rejeitados.
- Códigos próprios que diferem apenas por maiúsculas e minúsculas são tratados como o mesmo código, para evitar colisões de identificação.
- O alfabeto dos códigos exclui caracteres visualmente ambíguos: 0, 1, I, L e O, independentemente de maiúsculas ou minúsculas; entradas são normalizadas para maiúsculas antes da validação.
- Uma colisão durante a geração automática não pode criar códigos duplicados nem substituir um link existente; a geração deve produzir outro código disponível.
- Um link cuja expiração coincide com o instante do acesso é considerado expirado e não é contado.
- Acessos simultâneos não podem fazer a contagem perder incrementos nem reduzi-la.
- Uma data de expiração já passada não pode resultar em um link ativo.
- Acessos repetidos e automatizados são contados individualmente quando resultam em encaminhamento válido; não há deduplicação por pessoa ou dispositivo.
- A criação de links não exige desafio visual nem aplica limite específico de frequência ou automação.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir criar um link curto para uma URL de destino válida que use HTTP ou HTTPS e tenha no máximo 2048 caracteres.
- **FR-002**: Quando o criador não informar um código, o sistema MUST gerar automaticamente um código com exatamente 7 caracteres alfanuméricos não ambíguos.
- **FR-003**: O sistema MUST permitir que o criador escolha um código alfanumérico com pelo menos 4 e no máximo 20 caracteres, sem os caracteres visualmente ambíguos 0, 1, I, L e O, independentemente de maiúsculas ou minúsculas.
- **FR-004**: Cada código MUST identificar no máximo um link em todo o sistema; uma solicitação de código próprio já utilizado MUST ser rejeitada sem alterar o link existente.
- **FR-005**: O sistema MUST devolver ao criador o link curto completo para compartilhamento após a criação bem-sucedida.
- **FR-006**: O sistema MUST permitir que o criador informe uma data de expiração opcional; uma data já passada MUST ser rejeitada.
- **FR-007**: Ao receber acesso a um link existente e não expirado, o sistema MUST encaminhar a pessoa à URL de destino e incrementar a contagem desse link em exatamente um.
- **FR-008**: Um link expirado MUST NOT encaminhar a pessoa ao destino nem incrementar sua contagem.
- **FR-009**: O total de acessos MUST ser monotônico, preservado entre reinicializações e atualizado sem perda de incrementos em acessos simultâneos.
- **FR-010**: O sistema MUST permitir que qualquer pessoa consulte a contagem de acessos de um link existente usando seu código, sem autenticação ou segredo separado, e informar quando o código não existir.
- **FR-011**: A contagem MUST representar o número total de acessos que resultaram em encaminhamento válido, incluindo acessos repetidos, sem deduplicação por pessoa ou dispositivo.
- **FR-012**: O sistema MUST rejeitar entradas inválidas sem criar links e informar ao criador o motivo da rejeição em linguagem clara.
- **FR-013**: O sistema MUST permitir a criação de links sem exigir desafio visual nem aplicar limite específico de frequência ou automação.

### Key Entities *(include if feature involves data)*

- **Link curto**: Representa a associação entre um código único e uma URL de destino, com data de expiração opcional e contagem acumulada de acessos.
- **Acesso válido**: Representa um acesso a um link existente e ainda não expirado que resulta em encaminhamento ao destino e incrementa a contagem uma vez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em teste com dados válidos, 100% dos links criados sem código escolhido têm um código de exatamente 7 caracteres alfanuméricos; 100% dos códigos próprios aceitos têm entre 4 e 20 caracteres.
- **SC-002**: Em 100% dos testes de acesso a links ativos, a pessoa chega ao destino correto e a contagem aumenta exatamente uma vez por acesso concluído.
- **SC-003**: Em 100% dos testes de links expirados, nenhum acesso é encaminhado ao destino e nenhuma contagem é incrementada.
- **SC-004**: Em 100% dos testes após reinicialização, os links previamente criados continuam disponíveis, seus destinos e expirações são preservados e suas contagens não diminuem.
- **SC-005**: Pelo menos 90% das pessoas em um teste de uso conseguem criar e copiar um link curto válido em até um minuto e localizar sua contagem sem ajuda.

## Assumptions

- Os códigos aceitam apenas letras ASCII e dígitos não ambíguos; diferenças entre maiúsculas e minúsculas não criam códigos distintos.
- A expiração é avaliada pelo instante informado em relação ao horário atual; o link é considerado expirado no instante da expiração.
- Cada acesso que resulta em encaminhamento válido conta uma vez, incluindo acessos repetidos ou automatizados; não há filtragem de robôs nem deduplicação.
- Os erros de entrada são apresentados em português, conforme os padrões do projeto.

## Out of Scope

- Cadastro ou login de usuários.
- Domínio personalizado.
- Relatórios geográficos.
- Edição do destino de um link após sua criação.
