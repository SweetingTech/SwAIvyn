from alembic import op
import sqlalchemy as sa

revision = '002_add_gpu_columns'
down_revision = '001_init_settings_characters_chat'
branch_labels = None
depends_on = None


def upgrade():
    with op.batch_alter_table('connection_settings') as batch_op:
        batch_op.add_column(sa.Column('TtsGpu', sa.String(length=8), nullable=True))
        batch_op.add_column(sa.Column('SttGpu', sa.String(length=8), nullable=True))


def downgrade():
    with op.batch_alter_table('connection_settings') as batch_op:
        batch_op.drop_column('SttGpu')
        batch_op.drop_column('TtsGpu')

