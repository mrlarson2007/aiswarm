import pytest

# A placeholder for the UI framework we might use
class MockUI:
    def __init__(self):
        self.elements = {}

    def add_button(self, id, text):
        self.elements[id] = {"type": "button", "text": text}

    def get_element(self, id):
        return self.elements.get(id)

def create_login_button(ui):
    """This function should create a login button."""
    ui.add_button("login_button", "Login")

def test_login_button_is_created():
    """Tests that the create_login_button function actually creates a button."""
    ui = MockUI()
    create_login_button(ui)
    
    button = ui.get_element("login_button")
    
    assert button is not None, "The login button was not found!"
    assert button["text"] == "Login", "The button text is incorrect!"
