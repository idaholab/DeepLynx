# 1. Configure your .env
### Create a .env file and copy the contents of the .env_sample.
- Ensure the url is accurate. The URL in the .env_sample is already configured for local testing
- Add your API Key
- Add your API Secret

# 2. Ensure the user account is a System Admin
### Check the users table and confirm is_sys_admin == True

# 3. Create the Virtual Environment
### Run the following command in your project's root directory:
- Mac: `python3 -m venv venv`
- Windows: `python -m venv venv`

This creates a new folder named venv which contains a private copy of the Python interpreter and associated tools.
# 4. Activate the Virtual Environment
You must activate the environment in every new terminal session before installing packages or running your code.
Run the command corresponding to your operating system:
- Mac: `source venv/bin/activate`
- Windows: `venv\Scripts\activate`

# 5. Install Dependencies (and pytest)
Now that the environment is active, install the necessary libraries.
- Mac: `pip3 install -r requirements.txt`
- Windows: `pip install -r requirements.txt`

# 6. Run the tests
Now run `pytest` to run all tests

Or `pytest tests/<filename>` to run one test file