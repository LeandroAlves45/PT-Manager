using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exercise_videos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    object_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    declared_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    stored_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    stored_etag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    duration_milliseconds = table.Column<long>(type: "bigint", nullable: true),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    video_codec = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    audio_codec = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    upload_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processing_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ready_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_videos", x => x.id);
                    table.CheckConstraint("ck_exercise_videos_content_type", "content_type IN ('video/mp4', 'video/quicktime')");
                    table.CheckConstraint("ck_exercise_videos_declared_size", "declared_size_bytes BETWEEN 1 AND 104857600");
                    table.CheckConstraint("ck_exercise_videos_failure_code", "(status IN ('rejected', 'failed')) = (failure_code IS NOT NULL)");
                    table.CheckConstraint("ck_exercise_videos_ready_metadata", "status <> 'ready' OR (duration_milliseconds > 0 AND width > 0 AND height > 0 AND video_codec IS NOT NULL AND ready_at IS NOT NULL)");
                    table.CheckConstraint("ck_exercise_videos_status", "status IN ('pending', 'processing', 'ready', 'rejected', 'failed')");
                    table.CheckConstraint("ck_exercise_videos_upload_confirmed", "status NOT IN ('processing', 'ready') OR (stored_size_bytes = declared_size_bytes AND stored_etag IS NOT NULL AND processing_started_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_exercise_videos_exercise",
                        column: x => x.exercise_id,
                        principalTable: "exercises",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_exercise_videos_owner_trainer",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_exercise_videos_owner_status",
                table: "exercise_videos",
                columns: new[] { "owner_trainer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_exercise_videos_in_flight",
                table: "exercise_videos",
                column: "exercise_id",
                unique: true,
                filter: "status IN ('pending', 'processing')");

            migrationBuilder.CreateIndex(
                name: "uq_exercise_videos_object_key",
                table: "exercise_videos",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_exercise_videos_ready",
                table: "exercise_videos",
                column: "exercise_id",
                unique: true,
                filter: "status = 'ready'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preflight manual: remover a tabela esqueceria objetos privados ainda
            // guardados no R2 e deixaria durable jobs de vídeo sem handler, que
            // terminariam em dead letter. O rollback exige primeiro remover os
            // vídeos pela aplicação e deixar os jobs de eliminação concluir.
            migrationBuilder.Sql("""
                DO $preflight$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM exercise_videos
                        WHERE status IN ('pending', 'processing', 'ready')) THEN
                        RAISE EXCEPTION 'Rollback blocked: exercise_videos still references stored objects. Remove the videos through the application first.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM durable_jobs
                        WHERE job_type IN ('exercise-video.process', 'exercise-video.expire', 'exercise-video.delete-object')
                            AND status IN ('pending', 'processing', 'failed')) THEN
                        RAISE EXCEPTION 'Rollback blocked: exercise video durable jobs are still pending.';
                    END IF;
                END
                $preflight$;
                """);

            migrationBuilder.DropTable(
                name: "exercise_videos");
        }
    }
}
