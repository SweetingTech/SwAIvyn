"""Add push_subscriptions table for Web Push notifications

Revision ID: 006_push_subscriptions
Revises: 005_datetime_timestamps
Create Date: 2026-04-08 00:00:00.000000

Adds a push_subscriptions table that stores Web Push (VAPID) endpoint
and key material per user so the BFF can deliver push notifications when
agent tasks complete or scheduled workflows fire.
"""
from alembic import op
import sqlalchemy as sa

revision = '006_push_subscriptions'
down_revision = '005_datetime_timestamps'
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        'push_subscriptions',
        sa.Column('id', sa.String(128), primary_key=True),
        sa.Column('user_id', sa.String(64), sa.ForeignKey('users.id', ondelete='CASCADE'), nullable=False),
        sa.Column('endpoint', sa.Text(), nullable=False),
        sa.Column('p256dh', sa.Text(), nullable=False),
        sa.Column('auth', sa.Text(), nullable=False),
        sa.Column('created_at', sa.DateTime(timezone=True), nullable=False),
    )
    op.create_index('ix_push_subscriptions_user_id', 'push_subscriptions', ['user_id'])
    op.create_index('ix_push_subscriptions_endpoint', 'push_subscriptions', ['endpoint'], unique=True)


def downgrade():
    op.drop_index('ix_push_subscriptions_endpoint', table_name='push_subscriptions')
    op.drop_index('ix_push_subscriptions_user_id', table_name='push_subscriptions')
    op.drop_table('push_subscriptions')
