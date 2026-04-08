"""Add avatar_stats and room_items tables for 3D avatar phase

Revision ID: 006_avatar_3d_stats_room
Revises: 005_datetime_timestamps
Create Date: 2026-04-08 00:00:00.000000

Adds:
  - avatar_stats  – per-user Tamagotchi-like stat storage (energy, mood, relationship_score)
  - room_items    – per-user persistent room item list (JSON array of item IDs)
"""
from alembic import op
import sqlalchemy as sa

revision = '006_avatar_3d_stats_room'
down_revision = '005_datetime_timestamps'
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        'avatar_stats',
        sa.Column('user_id', sa.String(length=64), primary_key=True),
        sa.Column('energy', sa.Float(), nullable=False, server_default='80'),
        sa.Column('mood', sa.Float(), nullable=False, server_default='70'),
        sa.Column('relationship_score', sa.Float(), nullable=False, server_default='50'),
        sa.Column('updated_at', sa.DateTime(timezone=True), nullable=True),
    )

    op.create_table(
        'room_items',
        sa.Column('user_id', sa.String(length=64), primary_key=True),
        # JSON array of active item IDs, e.g. ["plant", "lamp"]
        sa.Column('items', sa.Text(), nullable=False, server_default='[]'),
        sa.Column('updated_at', sa.DateTime(timezone=True), nullable=True),
    )


def downgrade():
    op.drop_table('room_items')
    op.drop_table('avatar_stats')
