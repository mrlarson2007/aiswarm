import pytest
from click.testing import CliRunner
from aiswarm_cli import main
import os
import json
from unittest.mock import patch, mock_open

@pytest.fixture
def runner():
    return CliRunner()

