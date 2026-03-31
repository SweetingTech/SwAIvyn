"""Convert timestamp columns from String to DateTime

Revision ID: 005_datetime_timestamps
Revises: 004_add_external_agent_tables
Create Date: 2026-03-31 00:00:00.000000

Converts all ISO-8601 string timestamp columns across conversations, messages,
agents, agent_registry, agent_tasks, and agent_results to proper TIMESTAMP columns.
Existing ISO-format string data is cast automatically by PostgreSQL.
"""
from alembic import op
import sqlalchemy as sa

revision = '005_datetime_timestamps'
down_revision = '004_add_external_agent_tables'
branch_labels = None
depends_on = None


def upgrade():
    # conversations
    op.alter_column('conversations', 'created_at',
                    existing_type=sa.String(40),
                    type_=sa.DateTime(),
                    existing_nullable=False,
                    postgresql_using='created_at::timestamp')
    op.alter_column('conversations', 'last_updated',
                    existing_type=sa.String(40),
                    type_=sa.DateTime(),
                    existing_nullable=False,
                    postgresql_using='last_updated::timestamp')

    # messages
    op.alter_column('messages', 'timestamp',
                    existing_type=sa.String(40),
                    type_=sa.DateTime(),
                    existing_nullable=False,
                    postgresql_using='timestamp::timestamp')

    # agents
    op.alter_column('agents', 'created_at',
                    existing_type=sa.String(40),
                    type_=sa.DateTime(),
                    existing_nullable=False,
                    postgresql_using='created_at::timestamp')
    op.alter_column('agents', 'updated_at',
                    existing_type=sa.String(40),
                    type_=sa.DateTime(),
                    existing_nullable=False,
                    postgresql_using='updated_at::timestamp')

    # agent_registry
    op.alter_column('agent_registry', 'last_heartbeat',
                    existing_type=sa.String(40),
                    type_=sa.DateTime(),
                    existing_nullable=True,
                    postgresql_using='last_heartbeat::timestamp')
    op.alter_column('agent_registry', 'created_at',
                    existing_type=sa.String(40),
                    type_=sa.DateTime(),
                    existing_nullable=False,
                    postgresql_using='created_at::timestamp')
    op.alter_column('agent_registry', 'updated_at',
                    existing_type=sa.String(40),
                    type_=sa.DateTime(),
                    existing_nullable=False,
                    postgresql_using='updated_at::timestamp')

    # agent_tasks
    for col in ('created_at', 'updated_at'):
        op.alter_column('agent_tasks', col,
                        existing_type=sa.String(40),
                        type_=sa.DateTime(),
                        existing_nullable=False,
                        postgresql_using=f'{col}::timestamp')
    for col in ('started_at', 'completed_at', 'estimated_completion'):
        op.alter_column('agent_tasks', col,
                        existing_type=sa.String(40),
                        type_=sa.DateTime(),
                        existing_nullable=True,
                        postgresql_using=f'{col}::timestamp')

    # agent_results
    op.alter_column('agent_results', 'created_at',
                    existing_type=sa.String(40),
                    type_=sa.DateTime(),
                    existing_nullable=False,
                    postgresql_using='created_at::timestamp')


def downgrade():
    # Reverse: cast DateTime back to text
    for table, col, nullable in [
        ('agent_results', 'created_at', False),
        ('agent_tasks', 'estimated_completion', True),
        ('agent_tasks', 'completed_at', True),
        ('agent_tasks', 'started_at', True),
        ('agent_tasks', 'updated_at', False),
        ('agent_tasks', 'created_at', False),
        ('agent_registry', 'updated_at', False),
        ('agent_registry', 'created_at', False),
        ('agent_registry', 'last_heartbeat', True),
        ('agents', 'updated_at', False),
        ('agents', 'created_at', False),
        ('messages', 'timestamp', False),
        ('conversations', 'last_updated', False),
        ('conversations', 'created_at', False),
    ]:
        op.alter_column(table, col,
                        existing_type=sa.DateTime(),
                        type_=sa.String(40),
                        existing_nullable=nullable,
                        postgresql_using=f"{col}::text")
