from alembic import op
import sqlalchemy as sa

revision = '001_init_settings_characters_chat'
down_revision = None
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        'chat_settings',
        sa.Column('user_id', sa.String(length=64), primary_key=True),
        sa.Column('llm_engine', sa.String(length=32), nullable=False, server_default='ollama'),
        sa.Column('llm_model', sa.String(length=200), nullable=True),
        sa.Column('tts_provider', sa.String(length=64), nullable=True),
        sa.Column('tts_voice_id', sa.String(length=100), nullable=True),
        sa.Column('enabled_engines', sa.Text(), nullable=True),
        sa.Column('engine_models', sa.Text(), nullable=True),
    )

    op.create_table(
        'connection_settings',
        sa.Column('user_id', sa.String(length=64), primary_key=True),
        sa.Column('OpenAiApiKey', sa.Text(), nullable=True),
        sa.Column('ClaudeApiKey', sa.Text(), nullable=True),
        sa.Column('ClaudeApiUrl', sa.Text(), nullable=True),
        sa.Column('OllamaApiUrl', sa.Text(), nullable=True),
        sa.Column('LmStudioApiUrl', sa.Text(), nullable=True),
        sa.Column('EnableStreaming', sa.Boolean(), nullable=False, server_default=sa.text('true')),
    )

    op.create_table(
        'characters',
        sa.Column('id', sa.String(length=100), primary_key=True),
        sa.Column('user_id', sa.String(length=64), nullable=True),
        sa.Column('name', sa.String(length=200), nullable=False),
        sa.Column('system_prompt', sa.Text(), nullable=True),
        sa.Column('image_path', sa.Text(), nullable=True),
    )

    op.create_table(
        'conversations',
        sa.Column('id', sa.String(length=64), primary_key=True),
        sa.Column('user_id', sa.String(length=64), nullable=False),
        sa.Column('title', sa.String(length=300), nullable=False),
        sa.Column('folder_id', sa.String(length=64), nullable=True),
        sa.Column('created_at', sa.String(length=40), nullable=False),
        sa.Column('last_updated', sa.String(length=40), nullable=False),
    )

    op.create_table(
        'messages',
        sa.Column('id', sa.String(length=64), primary_key=True),
        sa.Column('conversation_id', sa.String(length=64), nullable=False),
        sa.Column('role', sa.String(length=32), nullable=False),
        sa.Column('content', sa.Text(), nullable=False),
        sa.Column('timestamp', sa.String(length=40), nullable=False),
    )


def downgrade():
    op.drop_table('messages')
    op.drop_table('conversations')
    op.drop_table('characters')
    op.drop_table('connection_settings')
    op.drop_table('chat_settings')

