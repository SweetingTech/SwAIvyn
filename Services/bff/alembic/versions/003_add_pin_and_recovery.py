"""add pin and recovery

Revision ID: 003
Revises: 002
Create Date: 2025-09-06 12:00:00.000000

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = '003'
down_revision = '002_add_gpu_columns'
branch_labels = None
depends_on = None


def upgrade():
    op.add_column('users', sa.Column('pin_hash', sa.String(length=200), nullable=True))
    op.add_column('users', sa.Column('recovery_codes_hash', sa.Text(), nullable=True))


def downgrade():
    op.drop_column('users', 'recovery_codes_hash')
    op.drop_column('users', 'pin_hash')
