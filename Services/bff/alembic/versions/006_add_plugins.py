"""Add plugins table

Revision ID: 006_add_plugins
Revises: 005_datetime_timestamps
Create Date: 2026-04-08 00:00:00.000000

Creates the plugins table for the first-class plugin system (Phase 5).
"""
from alembic import op
import sqlalchemy as sa

revision = '006_add_plugins'
down_revision = '005_datetime_timestamps'
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        'plugins',
        sa.Column('id', sa.String(128), primary_key=True),
        sa.Column('name', sa.String(200), nullable=False),
        sa.Column('version', sa.String(32), nullable=False),
        sa.Column('description', sa.Text, nullable=True),
        sa.Column('author', sa.String(200), nullable=True),
        sa.Column('manifest', sa.Text, nullable=False),
        sa.Column('entry_point', sa.String(500), nullable=True),
        sa.Column('permissions', sa.Text, nullable=True),
        sa.Column('status', sa.String(32), nullable=False, server_default=sa.text("'installed'")),
        sa.Column('health_endpoint', sa.String(500), nullable=True),
        sa.Column('health_status', sa.String(32), nullable=True),
        sa.Column('installed_by', sa.String(64), sa.ForeignKey('users.id', ondelete='SET NULL'), nullable=True),
        sa.Column('installed_at', sa.DateTime(timezone=True), nullable=False),
        sa.Column('updated_at', sa.DateTime(timezone=True), nullable=False),
    )


def downgrade():
    op.drop_table('plugins')
