"""Add external agent tables

Revision ID: 004_add_external_agent_tables
Revises: 003_add_pin_and_recovery
Create Date: 2025-09-15 21:30:00.000000

"""
from alembic import op
import sqlalchemy as sa

# revision identifiers
revision = '004_add_external_agent_tables'
down_revision = '003_add_pin_and_recovery'
branch_labels = None
depends_on = None

def upgrade():
    # External agent registry - tracks available agent services
    op.create_table('agent_registry',
        sa.Column('id', sa.String(128), primary_key=True),
        sa.Column('name', sa.String(200), nullable=False),
        sa.Column('description', sa.Text, nullable=True),
        sa.Column('capabilities', sa.Text, nullable=True),  # JSON array
        sa.Column('version', sa.String(32), nullable=True),
        sa.Column('health_endpoint', sa.String(500), nullable=True),
        sa.Column('api_key', sa.String(200), nullable=True),  # For agent authentication (hashed)
        sa.Column('last_heartbeat', sa.String(40), nullable=True),
        sa.Column('status', sa.String(32), nullable=False, server_default='available'),
        sa.Column('created_at', sa.String(40), nullable=False),
        sa.Column('updated_at', sa.String(40), nullable=False),
    )
    
    # Agent tasks - user-scoped tasks for proper isolation
    op.create_table('agent_tasks',
        sa.Column('id', sa.String(128), primary_key=True),
        sa.Column('agent_id', sa.String(128), sa.ForeignKey('agent_registry.id', ondelete='CASCADE'), nullable=False),
        sa.Column('user_id', sa.String(64), sa.ForeignKey('users.id', ondelete='CASCADE'), nullable=False),  # Required for isolation
        sa.Column('name', sa.String(200), nullable=False),
        sa.Column('description', sa.Text, nullable=True),
        sa.Column('status', sa.String(32), nullable=False, server_default='pending'),
        sa.Column('progress', sa.String(16), nullable=True),  # "75%" or "3/10"
        sa.Column('current_step', sa.String(300), nullable=True),
        sa.Column('input_data', sa.Text, nullable=True),  # JSON
        sa.Column('output_data', sa.Text, nullable=True),  # JSON
        sa.Column('error_message', sa.Text, nullable=True),
        sa.Column('priority', sa.String(16), nullable=False, server_default='normal'),
        sa.Column('created_at', sa.String(40), nullable=False),
        sa.Column('started_at', sa.String(40), nullable=True),
        sa.Column('completed_at', sa.String(40), nullable=True),
        sa.Column('estimated_completion', sa.String(40), nullable=True),
        sa.Column('updated_at', sa.String(40), nullable=False),
    )
    
    # Agent results - user-scoped results storage
    op.create_table('agent_results',
        sa.Column('id', sa.String(128), primary_key=True),
        sa.Column('task_id', sa.String(128), sa.ForeignKey('agent_tasks.id', ondelete='CASCADE'), nullable=False),
        sa.Column('user_id', sa.String(64), sa.ForeignKey('users.id', ondelete='CASCADE'), nullable=False),  # Explicit user isolation
        sa.Column('result_type', sa.String(32), nullable=False),  # 'file', 'insight', 'metric', 'data'
        sa.Column('name', sa.String(300), nullable=False),
        sa.Column('description', sa.Text, nullable=True),
        sa.Column('content', sa.Text, nullable=True),  # JSON content
        sa.Column('file_path', sa.String(500), nullable=True),  # Path to uploaded file
        sa.Column('file_size', sa.String(16), nullable=True),  # File size in bytes
        sa.Column('mime_type', sa.String(100), nullable=True),  # MIME type for files
        sa.Column('metadata', sa.Text, nullable=True),  # Additional JSON metadata
        sa.Column('created_at', sa.String(40), nullable=False),
    )
    
    # Create indexes for performance
    op.create_index('idx_agent_tasks_user_id', 'agent_tasks', ['user_id'])
    op.create_index('idx_agent_tasks_agent_id', 'agent_tasks', ['agent_id'])
    op.create_index('idx_agent_tasks_status', 'agent_tasks', ['status'])
    op.create_index('idx_agent_results_task_id', 'agent_results', ['task_id'])
    op.create_index('idx_agent_results_user_id', 'agent_results', ['user_id'])

def downgrade():
    op.drop_index('idx_agent_results_user_id')
    op.drop_index('idx_agent_results_task_id')
    op.drop_index('idx_agent_tasks_status')
    op.drop_index('idx_agent_tasks_agent_id')
    op.drop_index('idx_agent_tasks_user_id')
    op.drop_table('agent_results')
    op.drop_table('agent_tasks')
    op.drop_table('agent_registry')