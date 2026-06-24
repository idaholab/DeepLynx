using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class UserAdminInfoUseProjectAdminColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Project admin status is now the dedicated project_members.is_project_admin column
            // rather than being derived from the "Admin" role name, matching AdminService.ProjectAdminCheck.
            migrationBuilder.Sql(@"
					CREATE OR REPLACE FUNCTION deeplynx.get_user_admin_info(
						_user_id BIGINT,
						_organization_id BIGINT DEFAULT NULL,
						_project_id BIGINT DEFAULT NULL
					)
					RETURNS TABLE(
						id BIGINT,
						name TEXT,
						username TEXT,
						email TEXT,
						is_sys_admin BOOLEAN,
						is_archived BOOLEAN,
						is_active BOOLEAN,
						is_org_admin BOOLEAN,
						is_project_admin BOOLEAN
					)
					LANGUAGE plpgsql AS $$
					BEGIN
						RETURN QUERY
						SELECT DISTINCT u.id, u.name, u.username, u.email,
							u.is_sys_admin, u.is_archived, u.is_active,
							CASE -- make this null if org id not supplied
								WHEN _organization_id IS NULL THEN NULL
								ELSE COALESCE (ou.is_org_admin, FALSE)
							END AS is_org_admin,
							CASE -- make this null if project id not supplied
								WHEN _project_id IS NULL THEN NULL
								ELSE COALESCE(pm.is_project_admin, FALSE)
							END AS is_project_admin
						FROM deeplynx.users u
						LEFT JOIN deeplynx.organization_users ou
							ON u.id = ou.user_id
							AND _organization_id IS NOT NULL -- only join if org id supplied
							AND ou.organization_id = _organization_id
						LEFT JOIN deeplynx.group_users gu
							ON gu.user_id = u.id
							AND _project_id IS NOT NULL -- only join if project id supplied
						LEFT JOIN deeplynx.project_members pm
							ON (u.id = pm.user_id OR gu.group_id = pm.group_id)
							AND _project_id IS NOT NULL -- only join if project id supplied
							AND pm.project_id = _project_id
						WHERE u.id = _user_id;
					END;
					$$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the role-name based derivation of is_project_admin.
            migrationBuilder.Sql(@"
					CREATE OR REPLACE FUNCTION deeplynx.get_user_admin_info(
						_user_id BIGINT,
						_organization_id BIGINT DEFAULT NULL,
						_project_id BIGINT DEFAULT NULL
					)
					RETURNS TABLE(
						id BIGINT,
						name TEXT,
						username TEXT,
						email TEXT,
						is_sys_admin BOOLEAN,
						is_archived BOOLEAN,
						is_active BOOLEAN,
						is_org_admin BOOLEAN,
						is_project_admin BOOLEAN
					)
					LANGUAGE plpgsql AS $$
					BEGIN
						RETURN QUERY
						SELECT DISTINCT u.id, u.name, u.username, u.email,
							u.is_sys_admin, u.is_archived, u.is_active,
							CASE -- make this null if org id not supplied
								WHEN _organization_id IS NULL THEN NULL
								ELSE COALESCE (ou.is_org_admin, FALSE)
							END AS is_org_admin,
							CASE -- make this null if project id not supplied
								WHEN _project_id IS NULL THEN NULL
								ELSE COALESCE(r.name = 'Admin'::text, FALSE)
							END AS is_project_admin
						FROM deeplynx.users u
						LEFT JOIN deeplynx.organization_users ou
							ON u.id = ou.user_id
							AND _organization_id IS NOT NULL -- only join if org id supplied
							AND ou.organization_id = _organization_id
						LEFT JOIN deeplynx.group_users gu
							ON gu.user_id = u.id
							AND _project_id IS NOT NULL -- only join if project id supplied
						LEFT JOIN deeplynx.project_members pm
							ON (u.id = pm.user_id OR gu.group_id = pm.group_id)
							AND _project_id IS NOT NULL -- only join if project id supplied
							AND pm.project_id = _project_id
						LEFT JOIN deeplynx.roles r
							ON r.id = pm.role_id
							AND _project_id IS NOT NULL -- only join if project id supplied
						WHERE u.id = _user_id;
					END;
					$$;
            ");
        }
    }
}
