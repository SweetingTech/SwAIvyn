"""Add memories table

Revision ID: 006_add_memories
Revises: 005_datetime_timestamps
Create Date: 2026-04-08 00:00:00.000000

Adds per-user memory items table for the memory management UI.
"""
from alembic import op
import sqlalchemy as sa

revision = '006_add_memories'
down_revision = '005_datetime_timestamps'
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        'memories',
        sa.Column('id', sa.String(64), primary_key=True),
        sa.Column('user_id', sa.String(64), sa.ForeignKey('users.id', ondelete='CASCADE'), nullable=False),
        sa.Column('content', sa.Text(), nullable=False),
        sa.Column('category', sa.String(100), nullable=False, server_default='Personal'),
        sa.Column('is_shared', sa.Boolean(), nullable=False, server_default=sa.text('false')),
        sa.Column('annotation', sa.Text(), nullable=True),
        sa.Column('created_at', sa.DateTime(timezone=True), nullable=False),
        sa.Column('updated_at', sa.DateTime(timezone=True), nullable=False),
    )
    op.create_index('ix_memories_user_id', 'memories', ['user_id'])


def downgrade():
    op.drop_index('ix_memories_user_id', table_name='memories')
    op.drop_table('memories')
