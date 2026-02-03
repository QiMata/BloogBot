#include "../include/code_intelligence.h"
#include <iostream>

int main(int argc, char* argv[]) {
    std::cout << "Starting BloogBot C++ Code Intelligence MCP Server..." << std::endl;
    
    int port = 5002; // Default port
    if (argc > 1) {
        try {
            port = std::stoi(argv[1]);
        } catch (const std::exception& e) {
            std::cerr << "Invalid port number: " << argv[1] << std::endl;
            return 1;
        }
    }
    
    try {
        CodeIntelligence::MCPServer server(port);
        
        std::cout << "MCP Server listening on port " << port << std::endl;
        std::cout << "Press Ctrl+C to stop..." << std::endl;
        
        server.start();
    } catch (const std::exception& e) {
        std::cerr << "Error starting server: " << e.what() << std::endl;
        return 1;
    }
    
    return 0;
}
