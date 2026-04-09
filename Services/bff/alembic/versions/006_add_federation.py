"""Add Phase 4 federation, email, calendar, and browse-history tables

Revision ID: 006_add_federation
Revises: 005_datetime_timestamps
Create Date: 2026-04-08 00:00:00.000000

Adds the following tables required for cross-instance federation and the
email/calendar/web-browse integrations:
  - federation_peers
  - federated_messages
  - email_accounts
  - email_messages
  - calendar_accounts
  - calendar_events
  - browse_history
"""
from alembic import op
import sqlalchemy as sa

revision = '006_add_federation'
down_revision = '005_datetime_timestamps'
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        'federation_peers',
        sa.Column('id', sa.String(64), primary_key=True),
        sa.Column('name', sa.String(200), nullable=False),
        sa.Column('url', sa.String(500), nullable=False),
        sa.Column('api_key', sa.String(256), nullable=True),
        sa.Column('status', sa.String(32), nullable=False, server_default='pending'),
        sa.Column('discovered_via', sa.String(32), nullable=True),
        sa.Column('last_seen', sa.DateTime(timezone=True), nullable=True),
        sa.Column('created_at', sa.DateTime(timezone=True), nullable=False),
        sa.Column('updated_at', sa.DateTime(timezone=True), nullable=False),
    )

    op.create_table(
        'federated_messages',
        sa.Column('id', sa.String(64), primary_key=True),
        sa.Column('peer_id', sa.String(64), sa.ForeignKey('federation_peers.id', ondelete='SET NULL'), nullable=True),
        sa.Column('user_id', sa.String(64), sa.ForeignKey('users.id', ondelete='CASCADE'), nullable=False),
        sa.Column('direction', sa.String(8), nullable=False),
        sa.Column('message_type', sa.String(32), nullable=False),
        sa.Column('from_address', sa.String(300), nullable=True),
        sa.Column('to_address', sa.String(300), nullable=True),
        sa.Column('subject', sa.String(500), nullable=True),
        sa.Column('body', sa.Text, nullable=False),
        sa.Column('metadata', sa.Text, nullable=True),
        sa.Column('status', sa.String(32), nullable=False, server_default='sent'),
        sa.Column('created_at', sa.DateTime(timezone=True), nullable=False),
    )

    op.create_table(
        'email_accounts',
        sa.Column('id', sa.String(64), primary_key=True),
        sa.Column('user_id', sa.String(64), sa.ForeignKey('users.id', ondelete='CASCADE'), nullable=False),
        sa.Column('label', sa.String(200), nullable=False),
        sa.Column('host', sa.String(300), nullable=False),
        sa.Column('port', sa.String(8), nullable=False, server_default='993'),
        sa.Column('username', sa.String(300), nullable=False),
        sa.Column('password', sa.Text, nullable=True),
        sa.Column('use_ssl', sa.Boolean, nullable=False, server_default=sa.true()),
        sa.Column('last_synced', sa.DateTime(timezone=True), nullable=True),
        sa.Column('status', sa.String(32), nullable=False, server_default='unchecked'),
        sa.Column('created_at', sa.DateTime(timezone=True), nullable=False),
    )

    op.create_table(
        'email_messages',
        sa.Column('id', sa.String(128), primary_key=True),
        sa.Column('account_id', sa.String(64), sa.ForeignKey('email_accounts.id', ondelete='CASCADE'), nullable=False),
        sa.Column('user_id', sa.String(64), sa.ForeignKey('users.id', ondelete='CASCADE'), nullable=False),
        sa.Column('mailbox', sa.String(200), nullable=False, server_default='INBOX'),
        sa.Column('uid', sa.String(64), nullable=False),
        sa.Column('subject', sa.String(500), nullable=True),
        sa.Column('from_addr', sa.String(500), nullable=True),
        sa.Column('to_addr', sa.Text, nullable=True),
        sa.Column('date', sa.DateTime(timezone=True), nullable=True),
        sa.Column('body_text', sa.Text, nullable=True),
        sa.Column('is_read', sa.Boolean, nullable=False, server_default=sa.false()),
        sa.Column('flags', sa.Text, nullable=True),
        sa.Column('synced_at', sa.DateTime(timezone=True), nullable=False),
    )

    op.create_table(
        'calendar_accounts',
        sa.Column('id', sa.String(64), primary_key=True),
        sa.Column('user_id', sa.String(64), sa.ForeignKey('users.id', ondelete='CASCADE'), nullable=False),
        sa.Column('label', sa.String(200), nullable=False),
        sa.Column('url', sa.String(500), nullable=False),
        sa.Column('username', sa.String(300), nullable=True),
        sa.Column('password', sa.Text, nullable=True),
        sa.Column('type', sa.String(16), nullable=False, server_default='caldav'),
        sa.Column('color', sa.String(16), nullable=True),
        sa.Column('last_synced', sa.DateTime(timezone=True), nullable=True),
        sa.Column('status', sa.String(32), nullable=False, server_default='unchecked'),
        sa.Column('created_at', sa.DateTime(timezone=True), nullable=False),
    )

    op.create_table(
        'calendar_events',
        sa.Column('id', sa.String(128), primary_key=True),
        sa.Column('account_id', sa.String(64), sa.ForeignKey('calendar_accounts.id', ondelete='CASCADE'), nullable=False),
        sa.Column('user_id', sa.String(64), sa.ForeignKey('users.id', ondelete='CASCADE'), nullable=False),
        sa.Column('uid', sa.String(300), nullable=False),
        sa.Column('summary', sa.String(500), nullable=True),
        sa.Column('description', sa.Text, nullable=True),
        sa.Column('location', sa.String(500), nullable=True),
        sa.Column('start_dt', sa.DateTime(timezone=True), nullable=True),
        sa.Column('end_dt', sa.DateTime(timezone=True), nullable=True),
        sa.Column('all_day', sa.Boolean, nullable=False, server_default=sa.false()),
        sa.Column('recurrence', sa.Text, nullable=True),
        sa.Column('raw_ical', sa.Text, nullable=True),
        sa.Column('synced_at', sa.DateTime(timezone=True), nullable=False),
    )

    op.create_table(
        'browse_history',
        sa.Column('id', sa.String(64), primary_key=True),
        sa.Column('user_id', sa.String(64), sa.ForeignKey('users.id', ondelete='CASCADE'), nullable=False),
        sa.Column('url', sa.Text, nullable=False),
        sa.Column('title', sa.String(500), nullable=True),
        sa.Column('content_text', sa.Text, nullable=True),
        sa.Column('visited_at', sa.DateTime(timezone=True), nullable=False),
    )


def downgrade():
    op.drop_table('browse_history')
    op.drop_table('calendar_events')
    op.drop_table('calendar_accounts')
    op.drop_table('email_messages')
    op.drop_table('email_accounts')
    op.drop_table('federated_messages')
    op.drop_table('federation_peers')
