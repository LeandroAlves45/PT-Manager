using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedImageAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bloco nao gerado pelo EF. Um par incompleto ja existente faria o ADD
            // CONSTRAINT falhar com uma violacao generica a meio do deploy. O
            // preflight falha antes, com uma mensagem que diz o que corrigir, e
            // nao apaga dados: um URL sem identificador aponta para um asset que
            // nenhuma rotina consegue eliminar, e essa decisao e humana.
            migrationBuilder.Sql(
                """
                DO $preflight$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM trainer_settings
                        WHERE (logo_url IS NULL) <> (logo_public_id IS NULL)
                    ) THEN
                        RAISE EXCEPTION 'Migration blocked: trainer_settings has logo_url and logo_public_id out of pair.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM clients
                        WHERE avatar_url IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION 'Migration blocked: clients has avatar_url without a managed avatar_public_id.';
                    END IF;
                END
                $preflight$;
                """);

            migrationBuilder.AddColumn<string>(
                name: "avatar_public_id",
                table: "clients",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_trainer_settings_logo_pair",
                table: "trainer_settings",
                sql: "(logo_url IS NULL AND logo_public_id IS NULL) OR (logo_url IS NOT NULL AND logo_public_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_clients_avatar_pair",
                table: "clients",
                sql: "(avatar_url IS NULL AND avatar_public_id IS NULL) OR (avatar_url IS NOT NULL AND avatar_public_id IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bloco nao gerado pelo EF. Remover avatar_public_id com avatares
            // geridos ativos deixaria assets no storage sem qualquer referencia
            // que permitisse elimina-los. O rollback exige remover primeiro esses
            // avatares pela aplicacao, que agenda as eliminacoes pela outbox.
            migrationBuilder.Sql(
                """
                DO $preflight$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM clients
                        WHERE avatar_public_id IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION 'Rollback blocked: managed client avatars must be removed before dropping avatar_public_id.';
                    END IF;
                END
                $preflight$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_trainer_settings_logo_pair",
                table: "trainer_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_clients_avatar_pair",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "avatar_public_id",
                table: "clients");
        }
    }
}
