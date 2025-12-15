# 1. Configure your .env
### Create a .env file and copy the contents of the .env_sample.
- Ensure the url is accurate. The URL in the .env_sample is already configured for local testing
- Add your API Key
- Add your API Secret

# 2. Create the Virtual Environment
### Run the following command in your project's root directory:
bash
`python -m venv venv`
(mac may require `python3 -m venv venv`)

This creates a new folder named venv which contains a private copy of the Python interpreter and associated tools.
# 3. Activate the Virtual Environment
You must activate the environment in every new terminal session before installing packages or running your code.
Run the command corresponding to your operating system:
Windows (Command Prompt/PowerShell):
bash
`venv\Scripts\activate`

macOS/Linux (Bash/Zsh):
bash
`source venv/bin/activate`

# 4. Install Dependencies (and pytest)
Now that the environment is active, install the necessary libraries.
`pip install -r requirements.txt`
(mac may require `pip3 install -r requirements.txt`)

# 5. Run the tests
Now run `pytest` to run all tests

Or `pytest tests/<filename>` to run one test file